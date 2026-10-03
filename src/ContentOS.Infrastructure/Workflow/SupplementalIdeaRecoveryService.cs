using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Ideation;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentOS.Infrastructure.Workflow;

public sealed record SupplementalIdeaRecoveryResult(Guid AuditId, IReadOnlyList<Guid> IdeaRecordIds, IReadOnlyList<Guid> ContentIdeaIds, bool AlreadyRecorded);

/// <summary>Stores explicitly requested, source-bound offline recoveries through the normal pending-idea handler. Does not generate, approve or change historical step evidence.</summary>
public sealed class SupplementalIdeaRecoveryService(ContentOsDbContext db, FullWorkflowSpecificationStore store, IFullWorkflowStepHandler saveHandler, IIdeaDeduplicator deduplicator)
{
    public async Task<SupplementalIdeaRecoveryResult> RecoverAsync(Guid originalRunId,string preservedResponseJson,string expectedResponseHash,
        IReadOnlyList<CandidateContentIdea> offlineCandidates,IReadOnlyList<string> requestedTitles,CancellationToken cancellationToken)
    {
        static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var rawHash=Hash(preservedResponseJson);
        if(!rawHash.Equals(expectedResponseHash,StringComparison.OrdinalIgnoreCase)||requestedTitles.Count is <1 or >8||requestedTitles.Distinct(StringComparer.Ordinal).Count()!=requestedTitles.Count||saveHandler.CapabilityKey!="SaveIdeaRecords")
            throw new InvalidOperationException("Recovery requires exact preserved-response identity, explicit unique titles and the normal pending-idea handler.");
        var auditId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(originalRunId+"|"+rawHash+"|"+string.Join("|",requestedTitles.Order(StringComparer.Ordinal))))[..16]);
        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var receipt=await db.ContentArtifacts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==auditId,cancellationToken);
            if(receipt is not null)
            {
                if(receipt.ArtifactType!="SupplementalIdeaRecoveryAudit"||receipt.ContentWorkflowJobId!=originalRunId)throw new InvalidOperationException("Recovery receipt identity differs.");
                var previous=JsonSerializer.Deserialize<SupplementalIdeaRecoveryResult>(receipt.ContentJson)!;
                await transaction.CommitAsync(cancellationToken);
                return previous with{AlreadyRecorded=true};
            }
            var run=await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==originalRunId,cancellationToken);
            if(run.WorkflowType!=WorkflowDefinitionType.Idea||run.Status!=WorkflowDefinitionRunStatus.AwaitingHumanApproval||run.CompletedUtc is not null)throw new InvalidOperationException("Original production ideation must remain awaiting human approval.");
            var definition=await db.WorkflowDefinitions.AsNoTracking().SingleAsync(d=>d.Id==run.WorkflowDefinitionId,cancellationToken);
            if(definition.Version!=run.Version)throw new InvalidOperationException("Original definition version differs from the frozen run.");
            var spec=FullWorkflowSpecification.Parse(await store.GetSourceJsonAsync(run.WorkflowDefinitionId,cancellationToken));
            FullWorkflowProductionContract.Validate(spec);
            var sourceSave=spec.Actions.SelectMany(a=>a.Steps).Single(s=>s.CapabilityKey=="SaveIdeaRecords");
            var stepRuns=await db.WorkflowStepRuns.AsNoTracking().Where(s=>db.WorkflowActionRuns.Where(a=>a.WorkflowDefinitionRunId==originalRunId).Select(a=>a.Id).Contains(s.WorkflowActionRunId)).OrderBy(s=>s.Id).ToArrayAsync(cancellationToken);
            if(stepRuns.Length!=9||stepRuns.Any(s=>s.Status!=WorkflowDefinitionRunStatus.Completed||s.OutputSnapshotJson is null))throw new InvalidOperationException("Original nine-step evidence is incomplete.");
            var runHash=Hash(JsonSerializer.Serialize(run));var stepsHash=Hash(JsonSerializer.Serialize(stepRuns));
            using var originalOutput=JsonDocument.Parse(stepRuns.Single(s=>s.Name==sourceSave.Name).OutputSnapshotJson!);
            if(originalOutput.RootElement.GetProperty("isFixture").GetBoolean())throw new InvalidOperationException("Fixture outputs cannot be recovered into production pending ideas.");
            var original=originalOutput.RootElement.GetProperty("payload").Deserialize<IdeaWorkflowState>()!;
            if(original.FixtureEvidence||!original.LedgerCompared||original.Context.SiteId==Guid.Empty)throw new InvalidOperationException("Original evidence lacks production site/ledger provenance.");
            if(!await db.Sites.AnyAsync(s=>s.Id==original.Context.SiteId&&s.IsActive,cancellationToken))throw new InvalidOperationException("Original target site is missing or inactive.");
            var originalRecords=await db.IdeaRecords.AsNoTracking().Where(i=>original.SavedIdeaIds.Contains(i.Id)).OrderBy(i=>i.Id).ToArrayAsync(cancellationToken);
            if(originalRecords.Length!=original.SavedIdeaIds.Count||originalRecords.Any(i=>i.SourceWorkflowRunId!=run.Id||i.SiteId!=original.Context.SiteId))throw new InvalidOperationException("Original saved-record identity differs.");
            var recordsHash=Hash(JsonSerializer.Serialize(originalRecords));
            var response=JsonSerializer.Deserialize<IdeationResponse>(preservedResponseJson,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("Missing preserved response.");
            var balanced=ResearchEvidenceHandoff.BalanceCollectedSources(original.Findings);
            var originalLedger=await db.ContentIdeas.AsNoTracking().Where(i=>i.SiteId==original.Context.SiteId).OrderBy(i=>i.Id).ToListAsync(cancellationToken);
            var ledgerHash=Hash(JsonSerializer.Serialize(originalLedger));
            var ledger=originalLedger.Where(i=>i.Status!="Archived").ToList();
            var recovered=new List<CandidateContentIdea>();
            foreach(var title in requestedTitles)
            {
                var raw=response.Ideas.SingleOrDefault(i=>i.Title==title)??throw new InvalidOperationException("Requested title is not unique in the preserved response.");
                var candidate=offlineCandidates.SingleOrDefault(i=>i.Title==title)??throw new InvalidOperationException("Requested title did not survive offline validation.");
                if(original.Candidates.Any(c=>c.Title==title)||candidate.PrimaryKeyword!=raw.PrimaryKeyword.Trim()||candidate.AudiencePainPoint!=raw.AudiencePainPoint.Trim()||candidate.AudienceGoal!=raw.AudienceGoal.Trim()||candidate.SearchIntent!=raw.SearchIntent.Trim()||candidate.RecommendedAngle!=raw.RecommendedAngle.Trim()
                    ||string.IsNullOrWhiteSpace(candidate.AudiencePainPoint)||string.IsNullOrWhiteSpace(candidate.AudienceGoal)||string.IsNullOrWhiteSpace(candidate.SearchIntent)||string.IsNullOrWhiteSpace(candidate.RecommendedAngle)||candidate.PrimaryKeyword.Length>240||candidate.PrimaryKeyword.Contains("http://",StringComparison.OrdinalIgnoreCase)||candidate.PrimaryKeyword.Contains("https://",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Recovery changed supplied identity/reader intent or targets an originally saved candidate.");
                candidate.SupportingFindings=ResearchEvidenceHandoff.SelectCollectedSources(balanced,raw.SupportingSourceUrls);
                if(candidate.SupportingFindings.Count==0||deduplicator.IsTooSimilarToSource(candidate.Title,balanced.Select(f=>f.SourceTitle))||deduplicator.IsDuplicate(candidate,ledger)||deduplicator.IsDuplicate(candidate,recovered))throw new InvalidOperationException("Recovery lacks selected collected evidence or duplicates a published ledger candidate/source headline.");
                candidate.EditorialAssessment=EditorialRubricEvaluator.Evaluate(raw.EditorialAssessment,original.Context.IdeaEditorialRubric,candidate.SupportingFindings.Select(f=>f.SourceUrl).ToArray());
                candidate.OverallScore=candidate.EditorialQualityScore??0m;
                candidate.SecondaryKeywords=(raw.SecondaryKeywords??[]).Where(k=>!string.IsNullOrWhiteSpace(k)).Select(k=>k.Trim()).Where(k=>k.Length<=240&&!k.Contains("http",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
                candidate.Summary=IdeationAgent.BuildCandidateSummary(raw.AudiencePainPoint.Trim(),raw.AudienceGoal.Trim());
                candidate.ContentType=IdeationAgent.DetermineContentType(raw.Title,raw.PrimaryKeyword,raw.SearchIntent);
                candidate.ContentBucket=IdeationAgent.DetermineContentBucket(raw.SearchIntent);
                candidate.WhyNow=string.IsNullOrWhiteSpace(raw.WhyNow)?$"Timeliness and search demand for {candidate.PrimaryKeyword} are not established by the supplied evidence.":raw.WhyNow.Trim();
                candidate.SeoMeasurementStatus="Unknown";candidate.CompetitionEvidenceStatus="Unknown";
                recovered.Add(candidate);
            }
            var supplementalState=new IdeaWorkflowState{Context=original.Context,Findings=original.Findings,Candidates=recovered,TargetCount=recovered.Count,LedgerCompared=true,ApprovalStatus="PendingApproval"};
            var result=await saveHandler.ExecuteAsync(new(run.Id,auditId,spec,sourceSave,JsonSerializer.SerializeToElement(new{kind="ExplicitSupplementalRecovery",originalRunId=run.Id}),JsonSerializer.SerializeToElement(supplementalState),rawHash,1,false),cancellationToken);
            string[] runtimeChecks=["OutputPresent","OutputArtifactPersisted","InputVersionMatched","NotSimulated"];
            var handlerChecks=sourceSave.AcceptanceCheckKeys.Where(k=>!runtimeChecks.Contains(k,StringComparer.Ordinal)).ToArray();
            if(result.IsFixture||result.Output.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined||result.Checks.Count!=handlerChecks.Length||handlerChecks.Any(k=>!result.Checks.TryGetValue(k,out var check)||!check.Passed))throw new InvalidOperationException("Normal pending-idea storage acceptance failed; supplemental transaction will roll back.");
            var saved=result.Output.Deserialize<IdeaWorkflowState>()!;
            if(saved.SavedIdeaIds.Count!=requestedTitles.Count)throw new InvalidOperationException("Normal storage did not stage the exact supplemental candidate count.");
            var pending=db.IdeaRecords.Local.Where(i=>saved.SavedIdeaIds.Contains(i.Id)).ToArray();
            var content=db.ContentIdeas.Local.Where(i=>i.SiteId==original.Context.SiteId&&requestedTitles.Contains(i.Title)).ToArray();
            if(pending.Length!=requestedTitles.Count||content.Length!=requestedTitles.Count||pending.Any(i=>i.Status!=IdeaRecordStatus.Pending||i.ApprovedUtc is not null))throw new InvalidOperationException("Supplemental records are not exact, unapproved pending records.");
            foreach(var record in pending)
            {
                var snapshot=JsonNode.Parse(record.IdeaSnapshotJson)!.AsObject();
                snapshot["supplementalRecovery"]=JsonSerializer.SerializeToNode(new{kind="PreservedResponseRecoveryAfterAngleLabelRepair",auditId,originalRunId=run.Id,originalResponseHash=rawHash,originalRunHash=runHash,originalStepEvidenceHash=stepsHash,originalSavedCount=original.SavedIdeaIds.Count,
                    recoveredUtc=DateTime.UtcNow,newModelCalls=0,independentReview=false,sourceRelevanceVerified=false,measuredSeoStatus="Unknown",scoreKind="GeneratorSelfAssessment; proposal rubric; not measured SEO",originalRunOutputsChanged=false,approvalStatus="PendingApproval"});
                record.IdeaSnapshotJson=snapshot.ToJsonString();
            }
            var outcome=new SupplementalIdeaRecoveryResult(auditId,saved.SavedIdeaIds,content.Select(i=>i.Id).ToArray(),false);
            db.ContentArtifacts.Add(new(){Id=auditId,ContentWorkflowJobId=run.Id,ArtifactType="SupplementalIdeaRecoveryAudit",Title="Supplemental recovery: "+requestedTitles.Count+" additional pending ideas; historical outputs unchanged",ContentJson=JsonSerializer.Serialize(outcome),
                ContentText=JsonSerializer.Serialize(new{originalRunId=run.Id,rawHash,runHash,stepsHash,recordsHash,ledgerHash,requestedTitles,newModelCalls=0,articleStarted=false,automaticApproval=false,acceptanceChecks=result.Checks}),CreatedByAgent="ExplicitSupplementalRecovery",VersionNumber=1});
            await db.SaveChangesAsync(cancellationToken);
            var afterRun=await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==run.Id,cancellationToken);
            var afterSteps=await db.WorkflowStepRuns.AsNoTracking().Where(s=>stepRuns.Select(x=>x.Id).Contains(s.Id)).OrderBy(s=>s.Id).ToArrayAsync(cancellationToken);
            var afterRecords=await db.IdeaRecords.AsNoTracking().Where(i=>original.SavedIdeaIds.Contains(i.Id)).OrderBy(i=>i.Id).ToArrayAsync(cancellationToken);
            var originalLedgerIds=originalLedger.Select(i=>i.Id).ToArray();
            var afterLedger=await db.ContentIdeas.AsNoTracking().Where(i=>originalLedgerIds.Contains(i.Id)).OrderBy(i=>i.Id).ToListAsync(cancellationToken);
            if(Hash(JsonSerializer.Serialize(afterRun))!=runHash||Hash(JsonSerializer.Serialize(afterSteps))!=stepsHash||Hash(JsonSerializer.Serialize(afterRecords))!=recordsHash||Hash(JsonSerializer.Serialize(afterLedger))!=ledgerHash)throw new InvalidOperationException("Historical run, steps, records or ledger changed; supplemental transaction will roll back.");
            await transaction.CommitAsync(cancellationToken);return outcome;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;
        }
    }
}
