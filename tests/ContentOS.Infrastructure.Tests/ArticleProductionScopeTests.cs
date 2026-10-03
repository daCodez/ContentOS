using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Text.Json;
namespace ContentOS.Infrastructure.Tests;
public class ArticleProductionScopeTests
{
    [Test]
    public async Task ExhaustedBriefRetryCannotBeResetByAContractRepair()
    {
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Offline exhausted article",WorkflowType=WorkflowDefinitionType.Article};db.WorkflowDefinitionFamilies.Add(family);await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);var source=File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedArticleWorkflow.json"));var definition=await store.ImportDraftAsync(source,family.Id,"OfflineFixture",default);
        var runtime=new FullWorkflowStepRuntime(db,store,[]);var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);
        var firstDefinition=await db.WorkflowStepDefinitions.SingleAsync(s=>db.WorkflowActionDefinitions.Where(a=>a.WorkflowDefinitionId==definition.Id).Select(a=>a.Id).Contains(s.WorkflowActionDefinitionId)&&s.CapabilityKey=="BuildStrategyBrief");
        var step=await db.WorkflowStepRuns.SingleAsync(s=>s.WorkflowStepDefinitionId==firstDefinition.Id);step.Status=WorkflowDefinitionRunStatus.Failed;step.InputSnapshotJson="{\"attempt\":3}";step.ErrorMessage="Original brief failure";run.Status=WorkflowDefinitionRunStatus.Failed;await db.SaveChangesAsync();
        Assert.ThrowsAsync<InvalidOperationException>(()=>runtime.RetryFailedStepAsync(run.Id,default));
        await db.Entry(step).ReloadAsync();Assert.That(step.Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed));Assert.That(step.InputSnapshotJson,Is.EqualTo("{\"attempt\":3}"));Assert.That(step.ErrorMessage,Is.EqualTo("Original brief failure"));
    }
    [Test]
    public async Task SavedProductionArticleCannotProcessWithoutScopedAuthorization()
    {
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Scoped article",WorkflowType=WorkflowDefinitionType.Article};db.WorkflowDefinitionFamilies.Add(family);await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);
        var source=File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedArticleWorkflow.json"));
        var definition=await store.ImportDraftAsync(source,family.Id,"Test",default);
        var runtime=new FullWorkflowStepRuntime(db,store,[]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);
        // Simulate a persisted genuine run being opened by a normal app scope.
        using var frozen=JsonDocument.Parse(run.InputSnapshotJson);
        run.InputSnapshotJson=JsonSerializer.Serialize(new{fixtureMode=false,frozenInput=new{},specificationHash=frozen.RootElement.GetProperty("specificationHash").GetString()});await db.SaveChangesAsync();
        Assert.ThrowsAsync<InvalidOperationException>(()=>runtime.ProcessAsync(run.Id,default));
        Assert.That(await db.WorkflowStepRuns.CountAsync(s=>s.Status==WorkflowDefinitionRunStatus.InProgress),Is.Zero);
    }
}
