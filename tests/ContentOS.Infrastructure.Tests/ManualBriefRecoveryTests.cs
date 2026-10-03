using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;
using System.Text;
using ContentOS.Infrastructure.Articles;
namespace ContentOS.Infrastructure.Tests;
public class ManualBriefRecoveryTests
{
    private static string Hash(string value)=>FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(value));
    [TestCase("postClaimAcceptanceFailure")][TestCase("success")][TestCase("changedSource")][TestCase("wrongResponseHash")][TestCase("wrongRunHash")][TestCase("notStopped")][TestCase("wrongStep")][TestCase("missingApproval")][TestCase("paraphrase")]
    public async Task RecoveryIsAtomicBoundedAndKeepsExhaustedAttempts(string defect)
    {
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var source=JsonSerializer.Serialize(new{url="https://example.org/source",excerpt="Trust requires evidence that the reader can inspect."});
        var site=new Site{Id=Guid.NewGuid(),Name="OfflineHIP",Domain="https://guardwithhip.com",DefaultTone="Practical",IsActive=true};var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Offline article",WorkflowType=WorkflowDefinitionType.Article};
        var idea=new IdeaRecord{Id=Guid.NewGuid(),SiteId=site.Id,IdeaTitle="Read the reasons",ReaderProblem="The reader cannot understand the label.",SearchIntent="Informational",Status=IdeaRecordStatus.Approved,ApprovedBy="OfflineFixture",ApprovedUtc=DateTime.UtcNow,IdeaSnapshotJson=JsonSerializer.Serialize(new{primaryKeyword="explainable website check",audienceGoal="Understand the evidence",summary="Read the reasons",contentType="Article",secondaryKeywordsJson="[]",sourceSummaryJson=JsonSerializer.Serialize(new[]{source})})};db.AddRange(site,family,idea);await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);var specification=File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedArticleWorkflow.json"));if(defect=="postClaimAcceptanceFailure"){var changed=System.Text.Json.Nodes.JsonNode.Parse(specification)!;changed["Actions"]![0]!["Steps"]![0]!["AcceptanceCheckKeys"]!.AsArray().Add("UnsupportedRecoveryAcceptance");specification=changed.ToJsonString();}var definition=await store.ImportDraftAsync(specification,family.Id,"OfflineFixture",default);
        var runtime=new FullWorkflowStepRuntime(db,store,[]);var run=await runtime.StartFixtureAsync(definition.Id,JsonSerializer.Serialize(new{ideaId=idea.Id,tone="Practical",approvedAssetRoot=Path.GetTempPath(),targetWordCountMin=300,targetWordCountMax=600}),default);
        using(var frozen=JsonDocument.Parse(run.InputSnapshotJson))run.InputSnapshotJson=JsonSerializer.Serialize(new{fixtureMode=false,frozenInput=frozen.RootElement.GetProperty("frozenInput"),specificationHash=frozen.RootElement.GetProperty("specificationHash")});run.Status=WorkflowDefinitionRunStatus.Failed;run.ErrorMessage="Original brief failure";
        var stepDefinition=await db.WorkflowStepDefinitions.SingleAsync(s=>db.WorkflowActionDefinitions.Where(a=>a.WorkflowDefinitionId==definition.Id).Select(a=>a.Id).Contains(s.WorkflowActionDefinitionId)&&s.CapabilityKey=="BuildStrategyBrief");var step=await db.WorkflowStepRuns.SingleAsync(s=>s.WorkflowStepDefinitionId==stepDefinition.Id);step.Status=WorkflowDefinitionRunStatus.Failed;using var frozenInputDocument=JsonDocument.Parse(run.InputSnapshotJson);var frozenInput=frozenInputDocument.RootElement.GetProperty("frozenInput");var inputHash=Hash(JsonSerializer.Serialize(new{input=frozenInput,previous=(JsonElement?)null,specificationHash=Hash(specification),SourceId=FullWorkflowSpecification.Parse(specification).Actions[0].Steps[0].SourceId}));step.InputSnapshotJson=JsonSerializer.Serialize(new{attempt=3,inputHash,frozenInput,previousOutput=(JsonElement?)null});step.ErrorMessage="Original brief failure";await db.SaveChangesAsync();db.ChangeTracker.Clear();
        run=await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==run.Id);step=await db.WorkflowStepRuns.AsNoTracking().SingleAsync(s=>s.Id==step.Id);
        var legacy=new ContentBriefResult(idea.IdeaTitle,[idea.ReaderProblem,"Use a concrete decision and evidence."],["explainable website check"],["Trust requires evidence that the reader can inspect."],400,true);var raw=JsonSerializer.Serialize(legacy);
        var approval=new ManualBriefRecoveryApproval(Guid.NewGuid(),run.Id,step.Id,Hash(JsonSerializer.Serialize(run)),Hash(JsonSerializer.Serialize(step)),inputHash,Hash(idea.IdeaSnapshotJson),Hash(raw),Hash("proposal"),Hash("provenance"),"Eric: approved offline fixture",DateTime.UtcNow);
        switch(defect){case "changedSource":await db.IdeaRecords.Where(i=>i.Id==idea.Id).ExecuteUpdateAsync(u=>u.SetProperty(i=>i.IdeaSnapshotJson,"{}"));break;case "wrongResponseHash":approval=approval with{RawResponseHash=Hash("wrong")};break;case "wrongRunHash":approval=approval with{ExpectedRunHash=Hash("wrong")};break;case "wrongStep":approval=approval with{StepId=Guid.NewGuid()};break;case "missingApproval":approval=approval with{ApprovedBy=""};break;case "paraphrase":raw=JsonSerializer.Serialize(legacy with{Evidence=["Encryption proves every website is safe."]});approval=approval with{RawResponseHash=Hash(raw)};break;}
        var authorization=Substitute.For<IFullWorkflowProductionAuthorization>();var writer=Substitute.For<IWorkflowArticleWriter>();var search=Substitute.For<IResearchSearchClient>();var service=new ManualBriefRecoveryService(db,store,authorization,writer,search);
        if(defect!="success")
        {Assert.ThrowsAsync<InvalidOperationException>(()=>service.AdoptAsync(approval,raw,defect!="notStopped",default));Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="ManualBriefRecoveryReceipt"),Is.Zero);Assert.That((await db.WorkflowStepRuns.AsNoTracking().SingleAsync(s=>s.Id==step.Id)).Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed));return;}
        var result=await service.AdoptAsync(approval,raw,true,default);Assert.That(result.NewModelCalls,Is.Zero);Assert.That(result.AlreadyRecorded,Is.False);
        var actual=await db.WorkflowStepRuns.AsNoTracking().SingleAsync(s=>s.Id==step.Id);Assert.That(actual.Status,Is.EqualTo(WorkflowDefinitionRunStatus.Completed));Assert.That(actual.InputSnapshotJson,Is.EqualTo(step.InputSnapshotJson));
        var receipt=await db.ContentArtifacts.AsNoTracking().SingleAsync(a=>a.Id==approval.RecoveryId);Assert.That(receipt.ContentJson,Does.Contain("Original brief failure").And.Contain("originalAttempts").And.Contain("3"));
        Assert.That((await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==run.Id)).Status,Is.EqualTo(WorkflowDefinitionRunStatus.InProgress));
        Assert.That(writer.ReceivedCalls(),Is.Empty);Assert.That(search.ReceivedCalls(),Is.Empty);
        var again=await service.AdoptAsync(approval,raw,true,default);Assert.That(again.AlreadyRecorded,Is.True);Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="ManualBriefRecoveryReceipt"),Is.EqualTo(1));
    }
}
