using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContentOS.Infrastructure.Tests;

public sealed class SupplementalIdeaRecoveryTests
{
    private sealed record Scenario(ContentOsDbContext Db,FullWorkflowSpecificationStore Store,Guid RunId,string Raw,string Hash,List<CandidateContentIdea> Candidates,string[] Requested,IFullWorkflowStepHandler Save);
    private static async Task<Scenario> Setup(bool fixture=false)
    {
        using var file=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","HipIdeationSelectionReplay.json")));
        var context=file.RootElement.GetProperty("Context").Deserialize<ResearchContext>()!;
        var findings=file.RootElement.GetProperty("Findings").Deserialize<List<ResearchFinding>>()!;
        var candidates=file.RootElement.GetProperty("OfflineCandidates").Deserialize<List<CandidateContentIdea>>()!;
        var raw=file.RootElement.GetProperty("Response").GetRawText();var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        string[] requested=candidates.Where(c=>c.Title.StartsWith("When a Trust Score")||c.Title.StartsWith("Before You Enter a Password")).Select(c=>c.Title).ToArray();
        var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Recovery fixture",WorkflowType=WorkflowDefinitionType.Idea};db.WorkflowDefinitionFamilies.Add(family);
        db.Sites.Add(new(){Id=context.SiteId,Name="HIP recovery fixture",Domain=context.SiteUrl,IsActive=true});await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);
        var specification=await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedIdeaWorkflow.json"));
        var definition=await store.ImportDraftAsync(specification,family.Id,"OfflineFixture",default);
        var spec=FullWorkflowSpecification.Parse(specification);
        var run=new WorkflowDefinitionRun{Id=Guid.NewGuid(),WorkflowDefinitionId=definition.Id,WorkflowDefinitionFamilyId=family.Id,WorkflowType=WorkflowDefinitionType.Idea,Version=definition.Version,Status=WorkflowDefinitionRunStatus.AwaitingHumanApproval,InputSnapshotJson="{}"};db.WorkflowDefinitionRuns.Add(run);
        var saved=new List<Guid>();var kept=candidates.Where(c=>!requested.Contains(c.Title)).ToList();
        foreach(var candidate in kept)
        {
            var id=Guid.NewGuid();saved.Add(id);
            db.IdeaRecords.Add(new(){Id=id,SourceWorkflowRunId=run.Id,SourceWorkflowDefinitionId=definition.Id,SiteId=context.SiteId,IdeaTitle=candidate.Title,Status=IdeaRecordStatus.Pending,IdeaSnapshotJson="{\"original\":true}"});
            db.ContentIdeas.Add(new(){Id=Guid.NewGuid(),SiteId=context.SiteId,Title=candidate.Title,PrimaryKeyword=candidate.PrimaryKeyword,AudiencePainPoint=candidate.AudiencePainPoint,RecommendedAngle=candidate.RecommendedAngle,SearchIntent=candidate.SearchIntent,Status="Pending"});
        }
        var state=new IdeaWorkflowState{Context=context,Findings=findings,Candidates=kept,SavedIdeaIds=saved,TargetCount=25,LedgerCompared=true,FixtureEvidence=fixture};
        foreach(var sourceAction in spec.Actions)
        {
            var actionDefinition=await db.WorkflowActionDefinitions.SingleAsync(a=>a.WorkflowDefinitionId==definition.Id&&a.CapabilityKey==sourceAction.CapabilityKey);
            var action=new WorkflowActionRun{Id=Guid.NewGuid(),WorkflowDefinitionRunId=run.Id,WorkflowActionDefinitionId=actionDefinition.Id,Name=sourceAction.Name,Order=sourceAction.Order,Status=WorkflowDefinitionRunStatus.Completed};db.WorkflowActionRuns.Add(action);
            foreach(var step in sourceAction.Steps)
            {
                var stepDefinition=await db.WorkflowStepDefinitions.SingleAsync(s=>s.WorkflowActionDefinitionId==actionDefinition.Id&&s.CapabilityKey==step.CapabilityKey);
                db.WorkflowStepRuns.Add(new(){Id=Guid.NewGuid(),WorkflowActionRunId=action.Id,Name=step.Name,Order=step.Order,WorkflowStepDefinitionId=stepDefinition.Id,Status=WorkflowDefinitionRunStatus.Completed,OutputSnapshotJson=JsonSerializer.Serialize(new{payload=state,isFixture=fixture})});
            }
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var save=new IdeaWorkflowStepHandler("SaveIdeaRecords",db,[],Substitute.For<IIdeationAgent>(),Substitute.For<IResearchSearchClient>(),new IdeaDeduplicatorService());
        return new(db,store,run.Id,raw,hash,candidates,requested,save);
    }
    [Test]
    public async Task RecoveryAddsTwoPendingRecordsAndAuditWithoutChangingHistoricalEvidenceAndIsIdempotent()
    {
        var s=await Setup();await using var db=s.Db;
        var before=(await db.WorkflowStepRuns.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()).Select(x=>x.OutputSnapshotJson).ToArray();
        var service=new SupplementalIdeaRecoveryService(db,s.Store,s.Save,new IdeaDeduplicatorService());
        var result=await service.RecoverAsync(s.RunId,s.Raw,s.Hash,s.Candidates,s.Requested,default);
        Assert.That(await db.IdeaRecords.CountAsync(),Is.EqualTo(10));Assert.That(await db.ContentIdeas.CountAsync(),Is.EqualTo(10));
        Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="SupplementalIdeaRecoveryAudit"),Is.EqualTo(1));
        var recovered=await db.IdeaRecords.AsNoTracking().Where(i=>result.IdeaRecordIds.Contains(i.Id)).ToArrayAsync();
        Assert.That(recovered.All(i=>i.Status==IdeaRecordStatus.Pending&&i.ApprovedUtc is null&&i.SourceWorkflowRunId==s.RunId),Is.True);
        Assert.That(recovered.All(i=>i.IdeaSnapshotJson.Contains("supplementalRecovery")&&i.IdeaSnapshotJson.Contains("GeneratorSelfAssessment")&&i.IdeaSnapshotJson.Contains("Unknown")),Is.True);
        Assert.That((await db.WorkflowStepRuns.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()).Select(x=>x.OutputSnapshotJson),Is.EqualTo(before));
        Assert.That((await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync()).Status,Is.EqualTo(WorkflowDefinitionRunStatus.AwaitingHumanApproval));
        var repeated=await service.RecoverAsync(s.RunId,s.Raw,s.Hash,s.Candidates,s.Requested,default);
        Assert.That(repeated.AlreadyRecorded,Is.True);Assert.That(repeated.IdeaRecordIds,Is.EqualTo(result.IdeaRecordIds));Assert.That(await db.IdeaRecords.CountAsync(),Is.EqualTo(10));
    }
    [TestCase("wrongHash")][TestCase("changedReaderProblem")][TestCase("fixture")][TestCase("missingRawTitle")]
    public async Task InvalidRecoveryProvenanceOrIdentityCannotWritePendingRecords(string defect)
    {
        var s=await Setup(defect=="fixture");await using var db=s.Db;
        if(defect=="changedReaderProblem")s.Candidates.Single(c=>c.Title==s.Requested[0]).AudiencePainPoint="Invented different reader problem";
        var service=new SupplementalIdeaRecoveryService(db,s.Store,s.Save,new IdeaDeduplicatorService());
        Assert.ThrowsAsync<InvalidOperationException>(()=>service.RecoverAsync(s.RunId,s.Raw,defect=="wrongHash"?new string('0',64):s.Hash,s.Candidates,defect=="missingRawTitle"?["Invented title"]:s.Requested,default));
        Assert.That(await db.IdeaRecords.CountAsync(),Is.EqualTo(8));Assert.That(await db.ContentIdeas.CountAsync(),Is.EqualTo(8));Assert.That(await db.ContentArtifacts.CountAsync(),Is.Zero);
    }
    [Test]
    public async Task RecoveryReconstructsMetadataAndScoresFromPreservedResponseInsteadOfTrustingReplayEdits()
    {
        var s=await Setup();await using var db=s.Db;
        var altered=s.Candidates.Single(c=>c.Title==s.Requested[0]);altered.Summary="Invented adoption metrics";altered.ContentType="CommercialLandingPage";altered.SecondaryKeywords=["invented phrase","https://example.org/invented"];
        altered.EditorialAssessment=null;
        var service=new SupplementalIdeaRecoveryService(db,s.Store,s.Save,new IdeaDeduplicatorService());
        var result=await service.RecoverAsync(s.RunId,s.Raw,s.Hash,s.Candidates,s.Requested,default);
        var record=await db.IdeaRecords.AsNoTracking().SingleAsync(i=>result.IdeaRecordIds.Contains(i.Id)&&i.IdeaTitle==s.Requested[0]);
        using var snapshot=JsonDocument.Parse(record.IdeaSnapshotJson);
        Assert.That(snapshot.RootElement.GetProperty("summary").GetString(),Does.Not.Contain("Invented adoption metrics"));
        Assert.That(snapshot.RootElement.GetProperty("secondaryKeywordsJson").GetString(),Does.Not.Contain("invented phrase").And.Not.Contain("example.org/invented"));
        Assert.That(snapshot.RootElement.GetProperty("contentType").GetString(),Is.EqualTo("Article"));
        Assert.That(snapshot.RootElement.GetProperty("editorialQualityScore").GetDecimal(),Is.EqualTo(100m));
    }
    [Test]
    public async Task FailedNormalStorageAcceptanceRollsBackAllSupplementalEntities()
    {
        var s=await Setup();await using var db=s.Db;
        var service=new SupplementalIdeaRecoveryService(db,s.Store,new FailAfterStaging(s.Save),new IdeaDeduplicatorService());
        Assert.ThrowsAsync<InvalidOperationException>(()=>service.RecoverAsync(s.RunId,s.Raw,s.Hash,s.Candidates,s.Requested,default));
        Assert.That(await db.IdeaRecords.CountAsync(),Is.EqualTo(8));Assert.That(await db.ContentIdeas.CountAsync(),Is.EqualTo(8));Assert.That(await db.ContentArtifacts.CountAsync(),Is.Zero);Assert.That(db.ChangeTracker.Entries().Count(),Is.Zero);
    }
    private sealed class FailAfterStaging(IFullWorkflowStepHandler inner):IFullWorkflowStepHandler
    {
        public string CapabilityKey=>"SaveIdeaRecords";
        public async Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken token)
        {
            var result=await inner.ExecuteAsync(context,token);var checks=result.Checks.ToDictionary();var key=checks.Keys.First();checks[key]=checks[key] with{Passed=false};return result with{Checks=checks};
        }
    }
}
