using ContentOS.Application.Research;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
namespace ContentOS.Infrastructure.Workflow;
public sealed record ManualBriefRecoveryApproval(Guid RecoveryId,Guid RunId,Guid StepId,string ExpectedRunHash,string ExpectedStepHash,string InputHash,string ApprovedSnapshotHash,string RawResponseHash,string ProposalHash,string ProvenanceHash,string ApprovedBy,DateTime ApprovedUtc);
public sealed record ManualBriefRecoveryResult(Guid RecoveryId,Guid StepId,string OutputHash,bool AlreadyRecorded,int NewModelCalls);
public sealed class ManualBriefRecoveryService(ContentOsDbContext db,FullWorkflowSpecificationStore store,IFullWorkflowProductionAuthorization authorization,IWorkflowArticleWriter writer,IResearchSearchClient search)
{
    public async Task<ManualBriefRecoveryResult> AdoptAsync(ManualBriefRecoveryApproval approval,string preservedResponse,bool confirmedExecutorStopped,CancellationToken token)
    {
        if(approval.RecoveryId==Guid.Empty||approval.RunId==Guid.Empty||approval.StepId==Guid.Empty||new[]{approval.ExpectedRunHash,approval.ExpectedStepHash,approval.InputHash,approval.ApprovedSnapshotHash,approval.RawResponseHash,approval.ProposalHash,approval.ProvenanceHash}.Any(h=>h is null||!System.Text.RegularExpressions.Regex.IsMatch(h,"^[A-Fa-f0-9]{64}$"))||!confirmedExecutorStopped||string.IsNullOrWhiteSpace(approval.ApprovedBy)||approval.ApprovedUtc<DateTime.UtcNow.AddDays(-1)||approval.ApprovedUtc>DateTime.UtcNow.AddMinutes(5)||authorization is null||Hash(preservedResponse)!=approval.RawResponseHash)throw new InvalidOperationException("Explicit stopped-executor approval and exact preserved response are required.");
        var prior=await db.ContentArtifacts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==approval.RecoveryId,token);
        if(prior is not null)
        {
            using var saved=JsonDocument.Parse(prior.ContentJson!);var recorded=saved.RootElement.GetProperty("approval").Deserialize<ManualBriefRecoveryApproval>();
            if(prior.ArtifactType!="ManualBriefRecoveryReceipt"||prior.ContentWorkflowJobId!=approval.RunId||recorded!=approval)throw new InvalidOperationException("Recovery identity conflicts with existing receipt.");
            return new(approval.RecoveryId,approval.StepId,saved.RootElement.GetProperty("outputHash").GetString()!,true,0);
        }
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        try
        {
            var run=await db.WorkflowDefinitionRuns.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==approval.RunId,token)??throw new InvalidOperationException("Approved failed run is missing.");
            var step=await db.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==approval.StepId,token)??throw new InvalidOperationException("Approved failed brief is missing.");
            var action=await db.WorkflowActionRuns.AsNoTracking().SingleAsync(a=>a.Id==step.WorkflowActionRunId,token);
            if(run.Status!=WorkflowDefinitionRunStatus.Failed||step.Status!=WorkflowDefinitionRunStatus.Failed||action.WorkflowDefinitionRunId!=run.Id||Hash(JsonSerializer.Serialize(run))!=approval.ExpectedRunHash||Hash(JsonSerializer.Serialize(step))!=approval.ExpectedStepHash)throw new InvalidOperationException("Failed run/brief changed since the approved proposal.");
            var specificationJson=await store.GetSourceJsonAsync(run.WorkflowDefinitionId,token);var spec=FullWorkflowSpecification.Parse(specificationJson);
            using var frozen=JsonDocument.Parse(run.InputSnapshotJson);var input=frozen.RootElement.GetProperty("frozenInput").Clone();
            if(frozen.RootElement.GetProperty("fixtureMode").GetBoolean()||frozen.RootElement.GetProperty("specificationHash").GetString()!=Hash(specificationJson)||spec.WorkflowType!=WorkflowDefinitionType.Article)throw new InvalidOperationException("Genuine frozen article specification is required.");
            FullWorkflowProductionContract.Validate(spec,authorization);authorization.ValidateExecution(spec,input,run.Id);
            var sourceStep=spec.Actions[0].Steps[0];var definition=await db.WorkflowStepDefinitions.AsNoTracking().SingleAsync(s=>s.Id==step.WorkflowStepDefinitionId,token);
            var actionDefinition=await db.WorkflowActionDefinitions.AsNoTracking().SingleAsync(a=>a.Id==action.WorkflowActionDefinitionId,token);
            if(sourceStep.CapabilityKey!="BuildStrategyBrief"||definition.CapabilityKey!="BuildStrategyBrief"||definition.Order!=sourceStep.Order||actionDefinition.Order!=spec.Actions[0].Order)throw new InvalidOperationException("Recovery is limited to the first brief step.");
            if(await db.WorkflowStepRuns.AnyAsync(s=>s.Id!=step.Id&&db.WorkflowActionRuns.Where(a=>a.WorkflowDefinitionRunId==run.Id).Select(a=>a.Id).Contains(s.WorkflowActionRunId)&&s.Status!=WorkflowDefinitionRunStatus.Pending,token))throw new InvalidOperationException("Downstream steps already changed; no brief recovery is permitted.");
            using var stepInput=JsonDocument.Parse(step.InputSnapshotJson!);
            var recomputedInputHash=Hash(JsonSerializer.Serialize(new{input,previous=(JsonElement?)null,specificationHash=Hash(specificationJson),sourceStep.SourceId}));
            if(stepInput.RootElement.GetProperty("attempt").GetInt32()!=3||stepInput.RootElement.GetProperty("inputHash").GetString()!=approval.InputHash||approval.InputHash!=recomputedInputHash||stepInput.RootElement.GetProperty("previousOutput").ValueKind!=JsonValueKind.Null||Hash(JsonSerializer.Serialize(stepInput.RootElement.GetProperty("frozenInput")))!=Hash(JsonSerializer.Serialize(input)))throw new InvalidOperationException("Original exhausted attempt/input boundary changed.");
            var ideaId=input.GetProperty("ideaId").GetGuid();var idea=await db.IdeaRecords.AsNoTracking().SingleAsync(i=>i.Id==ideaId,token);
            if(Hash(idea.IdeaSnapshotJson)!=approval.ApprovedSnapshotHash||idea.Status!=IdeaRecordStatus.Approved||idea.ApprovedUtc is null||string.IsNullOrWhiteSpace(idea.ApprovedBy))throw new InvalidOperationException("Original approved source snapshot changed.");
            using var ideaSnapshot=JsonDocument.Parse(idea.IdeaSnapshotJson);var originalSources=JsonSerializer.Deserialize<List<string>>(ideaSnapshot.RootElement.GetProperty("sourceSummaryJson").GetString()!)!;
            var preserved=JsonSerializer.Deserialize<ContentBriefResult>(preservedResponse)??throw new InvalidOperationException("Preserved brief output is absent.");
            var response=SourceBoundArticleBriefContract.BindPreservedQuoteResponse(preserved,originalSources);
            _=SourceBoundArticleBriefContract.Validate(response,originalSources,idea.IdeaTitle,idea.ReaderProblem,input.GetProperty("targetWordCountMin").GetInt32(),input.GetProperty("targetWordCountMax").GetInt32());
            var claimed=await db.WorkflowStepRuns.Where(s=>s.Id==step.Id&&s.Status==WorkflowDefinitionRunStatus.Failed&&s.InputSnapshotJson==step.InputSnapshotJson&&s.OutputSnapshotJson==step.OutputSnapshotJson&&s.ErrorMessage==step.ErrorMessage).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.Status,WorkflowDefinitionRunStatus.InProgress),token);
            if(claimed!=1)throw new InvalidOperationException("Failed brief changed or another executor claimed it.");
            var model=new PreservedBriefOnlyLlmClient(response);var handler=new ArticlePlanningWorkflowStepHandler("BuildStrategyBrief",db,model,writer,search);
            var result=await handler.ExecuteAsync(new(run.Id,step.Id,spec,sourceStep,input,null,approval.InputHash,3,false),token);
            if(result.IsFixture||result.Output.ValueKind!=JsonValueKind.Object||model.Consumed!=1)throw new InvalidOperationException("Recovery did not produce an actual preserved-response brief output.");
            var checks=new Dictionary<string,StepAcceptanceEvidence>(result.Checks,StringComparer.Ordinal)
            {
                ["OutputPresent"]=new(true,"Real brief handler reprocessed the preserved native response without generation.",["sha256:"+approval.RawResponseHash]),
                ["InputVersionMatched"]=new(true,"Original frozen input and approved source snapshot verified.",["sha256:"+approval.InputHash]),
                ["NotSimulated"]=new(true,"A completed real native response was adopted through separately approved exact-reference recovery, not a fixture.",["recovery://"+approval.RecoveryId]),
                ["OutputArtifactPersisted"]=new(true,"Actual output, original failure and separate receipt persisted atomically.",["recovery://"+approval.RecoveryId])
            };
            foreach(var key in sourceStep.AcceptanceCheckKeys)if(!checks.TryGetValue(key,out var evidence)||!evidence.Passed||string.IsNullOrWhiteSpace(evidence.Explanation)||evidence.EvidenceReferences.Count==0)throw new InvalidOperationException("Recovered brief lacks acceptance evidence: "+key);
            var outputHash=Hash(JsonSerializer.Serialize(result.Output));var output=JsonSerializer.Serialize(new{payload=result.Output,acceptanceChecks=checks,inputHash=approval.InputHash,outputHash,isFixture=false,sourceStepId=sourceStep.SourceId,attempt=3,manualRecoveryReceiptId=approval.RecoveryId});
            var receipt=JsonSerializer.Serialize(new{approval,originalRun=run,originalAction=action,originalStep=step,originalAttempts=3,newModelCalls=0,originalRetryCounterReset=false,sourceReferenceBinding="Offline unique exact excerpt matches; controller-derived source IDs",outputHash,recoveredUtc=DateTime.UtcNow});
            db.ContentArtifacts.Add(new(){Id=approval.RecoveryId,ContentWorkflowJobId=run.Id,ArtifactType="ManualBriefRecoveryReceipt",Title="Explicit preserved brief recovery",StorageType="Database",ContentJson=receipt,ContentText=result.Output.GetRawText(),CreatedByAgent=approval.ApprovedBy,VersionNumber=3});
            db.ContentArtifacts.Add(new(){Id=Guid.NewGuid(),ContentWorkflowJobId=run.Id,ArtifactType="WorkflowStepEvidence",Title=step.Name,StorageType="Database",ContentJson=output,ContentText=result.Output.GetRawText(),CreatedByAgent="ManualPreservedBriefRecovery",VersionNumber=3});
            await db.SaveChangesAsync(token);
            await db.WorkflowStepRuns.Where(s=>s.Id==step.Id&&s.Status==WorkflowDefinitionRunStatus.InProgress).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.Status,WorkflowDefinitionRunStatus.Completed).SetProperty(s=>s.OutputSnapshotJson,output).SetProperty(s=>s.CompletedUtc,DateTime.UtcNow).SetProperty(s=>s.ErrorMessage,(string?)null),token);
            await db.WorkflowActionRuns.Where(a=>a.Id==action.Id).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.Status,WorkflowDefinitionRunStatus.InProgress).SetProperty(a=>a.ErrorMessage,(string?)null),token);
            await db.WorkflowDefinitionRuns.Where(r=>r.Id==run.Id&&r.Status==WorkflowDefinitionRunStatus.Failed).ExecuteUpdateAsync(u=>u.SetProperty(r=>r.Status,WorkflowDefinitionRunStatus.InProgress).SetProperty(r=>r.ErrorMessage,(string?)null),token);
            await transaction.CommitAsync(token);db.ChangeTracker.Clear();return new(approval.RecoveryId,step.Id,outputHash,false,0);
        }
        catch{await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;}
    }
    private static string Hash(string text)=>FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(text));
    private sealed class PreservedBriefOnlyLlmClient(SourceBoundArticleBriefResponse response):ILlmClient
    {
        private int consumed;public int Consumed=>consumed;
        public Task<T?> GenerateAsync<T>(string prompt,string? model=null,CancellationToken cancellationToken=default)
        {if(typeof(T)!=typeof(SourceBoundArticleBriefResponse)||Interlocked.Increment(ref consumed)!=1)throw new InvalidOperationException("Recovery permits exactly one preserved brief response and no other model operation.");return Task.FromResult((T?)(object)response);}
        public Task<string> GenerateAsync(string prompt,string? model=null,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Generation is forbidden in offline brief recovery.");
    }
}
