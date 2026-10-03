using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace ContentOS.Infrastructure.Tests;

public class FullWorkflowRuntimeRegressionTests
{
    private static string Spec(Guid family) => JsonSerializer.Serialize(new
    {
        Id=Guid.NewGuid(), WorkflowDefinitionFamilyId=family, WorkflowType="Idea", Name="Full specification fixture", Version=2, IsActive=true,
        RequiresHumanApproval=true, ScoringContract=new { ContractVersion="fixture-v1" }, FinalOutput=new { Status="PendingApproval" },
        FailureHandling=new { MaxRetries=2, RetryFailedStepOnly=true, PauseOnFailure=true },
        Actions=new[] { new { Id=Guid.NewGuid(), Name="Actual multi-step action", CapabilityKey="FixtureAction", Order=1, IsEnabled=true, Instructions="Fixture", ExpectedOutput="Retain this action output contract", Steps=new[]
        {
            new { Id=Guid.NewGuid(), Name="First", CapabilityKey="First", Order=1, IsEnabled=true, Instructions="First real operation", ExpectedOutput="First evidence", AcceptanceCheckKeys=new[]{"OutputPresent","OutputArtifactPersisted","InputVersionMatched","NotSimulated","SpecificEvidence"} },
            new { Id=Guid.NewGuid(), Name="Second", CapabilityKey="Second", Order=2, IsEnabled=true, Instructions="Second real operation", ExpectedOutput="Second evidence", AcceptanceCheckKeys=new[]{"OutputPresent","OutputArtifactPersisted","InputVersionMatched","NotSimulated","SpecificEvidence"} }
        }} }
    });

    private static async Task<(ContentOsDbContext Db, Guid Family)> Db()
    {
        var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily { Id=Guid.NewGuid(),Name="Idea fixtures",WorkflowType=WorkflowDefinitionType.Idea };
        db.WorkflowDefinitionFamilies.Add(family);await db.SaveChangesAsync();return(db,family.Id);
    }

    [Test]
    public async Task DraftImportPreservesFullJsonAndLogicalIdsWithoutActivationOrPrimaryKeyReuse()
    {
        var (db,family)=await Db();await using var cleanup=db;
        var json=Spec(family);var store=new FullWorkflowSpecificationStore(db);
        var first=await store.ImportDraftAsync(json,family,"Fixture",default);
        var second=await store.ImportDraftAsync(json,family,"Fixture",default);
        Assert.That(first.Id,Is.Not.EqualTo(second.Id));Assert.That(second.Version,Is.EqualTo(first.Version+1));
        Assert.That(await store.GetSourceJsonAsync(first.Id,default),Is.EqualTo(json));
        Assert.That(await db.WorkflowDefinitions.CountAsync(d=>d.IsActive),Is.Zero);
        Assert.That((await db.WorkflowDefinitionFamilies.SingleAsync()).ActiveWorkflowDefinitionId,Is.Null);
        Assert.That(await db.WorkflowStepDefinitions.CountAsync(),Is.EqualTo(4));
        Assert.That(await db.WorkflowDefinitionMutations.CountAsync(),Is.EqualTo(2));
    }

    [Test]
    public async Task UnsupportedStepBlocksActivationAndDoesNotMutateActiveHistory()
    {
        var (db,family)=await Db();await using var cleanup=db;
        var store=new FullWorkflowSpecificationStore(db);var draft=await store.ImportDraftAsync(Spec(family),family,"Fixture",default);
        Assert.ThrowsAsync<InvalidOperationException>(()=>store.ActivateAsync(draft.Id,[],"Fixture",default));
        Assert.That(draft.IsActive,Is.False);
    }

    [Test]
    public async Task ActualStepOutputsPersistIndividuallyAndFailedStepAloneRetries()
    {
        var (db,family)=await Db();await using var cleanup=db;
        var store=new FullWorkflowSpecificationStore(db);var definition=await store.ImportDraftAsync(Spec(family),family,"Fixture",default);
        var first=new Handler("First");var second=new Handler("Second") { FailFirst=true };
        var runtime=new FullWorkflowStepRuntime(db,store,[first,second]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{\"siteId\":\"fixture\"}",default);
        await runtime.ProcessAsync(run.Id,default);
        var steps=await db.WorkflowStepRuns.OrderBy(s=>s.Order).ToListAsync();
        Assert.That(steps[0].Status,Is.EqualTo(WorkflowDefinitionRunStatus.Completed));
        Assert.That(steps[0].OutputSnapshotJson,Does.Contain("actual-operation-First"));
        Assert.That(steps[1].Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed));
        Assert.That(first.Calls,Is.EqualTo(1));
        await runtime.RetryFailedStepAsync(run.Id,default);await runtime.ProcessAsync(run.Id,default);
        run=await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==run.Id);
        Assert.That(first.Calls,Is.EqualTo(1));Assert.That(second.Calls,Is.EqualTo(2));
        Assert.That(run.Status,Is.EqualTo(WorkflowDefinitionRunStatus.AwaitingHumanApproval));
        Assert.That(run.CompletedUtc,Is.Null);
        Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ContentWorkflowJobId==run.Id),Is.EqualTo(2));
        Assert.That(run.ErrorMessage,Does.Contain("Fixture").And.Contain("approval"));
    }

    [Test]
    public async Task MissingStepHandlerCannotCompleteAnActionOrCreateAStubOutput()
    {
        var (db,family)=await Db();await using var cleanup=db;
        var store=new FullWorkflowSpecificationStore(db);var definition=await store.ImportDraftAsync(Spec(family),family,"Fixture",default);
        var runtime=new FullWorkflowStepRuntime(db,store,[]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);await runtime.ProcessAsync(run.Id,default);
        Assert.That(run.Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed));
        Assert.That(await db.WorkflowStepRuns.CountAsync(s=>s.Status==WorkflowDefinitionRunStatus.Completed),Is.Zero);
        Assert.That(await db.ContentArtifacts.CountAsync(),Is.Zero);
        Assert.That(run.ErrorMessage,Does.Contain("Unsupported step"));
    }

    private sealed class Handler(string key):IFullWorkflowStepHandler
    {
        public string CapabilityKey=>key;public int Calls;public bool FailFirst;
        public Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
        {
            Calls++;if(FailFirst&&Calls==1)throw new InvalidOperationException("Fixture failure, retry this step only");
            return Task.FromResult(new FullWorkflowStepResult(JsonSerializer.SerializeToElement(new { result="actual-operation-"+key }),
                new Dictionary<string,StepAcceptanceEvidence>{{"SpecificEvidence",new(true,"Fixture operation really executed",["fixture://operation/"+key])}},true));
        }
    }

    [Test]
    public async Task TwoExecutorsCannotInvokeTheSameClaimedStepTwice()
    {
        var connection=$"Data Source=claims-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection).Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Claims",WorkflowType=WorkflowDefinitionType.Idea};db.WorkflowDefinitionFamilies.Add(family);await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);var definition=await store.ImportDraftAsync(Spec(family.Id),family.Id,"Fixture",default);
        var blocker=new BlockingHandler();var runtime=new FullWorkflowStepRuntime(db,store,[blocker,new Handler("Second")]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);var first=runtime.ProcessAsync(run.Id,default);
        await blocker.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var secondDb=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection).Options);
        await secondDb.Database.OpenConnectionAsync();var secondRuntime=new FullWorkflowStepRuntime(secondDb,new(secondDb),[blocker,new Handler("Second")]);
        await secondRuntime.ProcessAsync(run.Id,default);
        Assert.ThrowsAsync<InvalidOperationException>(()=>secondRuntime.RecoverInterruptedStepAsync(run.Id,false,default));
        Assert.That(blocker.Calls,Is.EqualTo(1));blocker.Release.TrySetResult();await first;
        Assert.That(await db.ContentArtifacts.CountAsync(),Is.EqualTo(2));
    }

    [Test]
    public async Task CancellationRequiresExplicitFailedStepRetryBeforeRepeatingTheOperation()
    {
        var(db,family)=await Db();await using var cleanup=db;var store=new FullWorkflowSpecificationStore(db);
        var definition=await store.ImportDraftAsync(Spec(family),family,"Fixture",default);using var cancellation=new CancellationTokenSource();
        var first=new CancellingHandler(cancellation);var runtime=new FullWorkflowStepRuntime(db,store,[first,new Handler("Second")]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);
        Assert.ThrowsAsync<OperationCanceledException>(()=>runtime.ProcessAsync(run.Id,cancellation.Token));
        Assert.That((await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync()).ErrorMessage,Does.Contain("outcome may be unknown"));
        await runtime.ProcessAsync(run.Id,default);Assert.That(first.Calls,Is.EqualTo(1));Assert.That(await db.ContentArtifacts.CountAsync(),Is.Zero);
        await runtime.RetryFailedStepAsync(run.Id,default);await runtime.ProcessAsync(run.Id,default);
        Assert.That(first.Calls,Is.EqualTo(2));Assert.That(await db.ContentArtifacts.CountAsync(),Is.EqualTo(2));
    }

    [Test]
    public async Task FailedAcceptanceRollsBackPendingIdeaRecordsAlongsideStepEvidence()
    {
        var(db,family)=await Db();await using var cleanup=db;var store=new FullWorkflowSpecificationStore(db);
        var definition=await store.ImportDraftAsync(Spec(family),family,"Fixture",default);
        var runtime=new FullWorkflowStepRuntime(db,store,[new FailingSaveHandler(db),new Handler("Second")]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);await runtime.ProcessAsync(run.Id,default);
        Assert.That(await db.IdeaRecords.CountAsync(),Is.Zero);Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="WorkflowStepEvidence"),Is.Zero);
        Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="WorkflowStepFailedEvidence"),Is.EqualTo(1));
        Assert.That((await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync()).Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed));
    }

    private sealed class BlockingHandler:IFullWorkflowStepHandler
    {
        public string CapabilityKey=>"First";public int Calls;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
        {Interlocked.Increment(ref Calls);Started.TrySetResult();await Release.Task.WaitAsync(cancellationToken);return await new Handler("First").ExecuteAsync(context,cancellationToken);}
    }
    [Test]
    public async Task DelayedStaleRetryCannotResetAnAlreadyClaimedStep()
    {
        var connection=$"Data Source=retry-claims-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection).Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Retry claims",WorkflowType=WorkflowDefinitionType.Idea};db.WorkflowDefinitionFamilies.Add(family);await db.SaveChangesAsync();
        var store=new FullWorkflowSpecificationStore(db);var definition=await store.ImportDraftAsync(Spec(family.Id),family.Id,"Fixture",default);
        var handler=new RetryBlockingHandler();var runtime=new FullWorkflowStepRuntime(db,store,[handler,new Handler("Second")]);
        var run=await runtime.StartFixtureAsync(definition.Id,"{}",default);await runtime.ProcessAsync(run.Id,default);
        var gate=new RetryTransactionGate();
        await using var staleDb=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection).AddInterceptors(gate).Options);
        var staleRuntime=new FullWorkflowStepRuntime(staleDb,new(staleDb),[handler,new Handler("Second")]);
        var staleRetry=staleRuntime.RetryFailedStepAsync(run.Id,default);await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await runtime.RetryFailedStepAsync(run.Id,default);var processing=runtime.ProcessAsync(run.Id,default);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));handler.Release.TrySetResult();await processing;
        // SQLite holds a write transaction during the handler. Let that claimed operation commit,
        // then release the retry caller that already read the old Failed snapshot.
        gate.Release.TrySetResult();
        Assert.ThrowsAsync<InvalidOperationException>(async()=>await staleRetry);
        await using var observer=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection).Options);
        Assert.That((await observer.WorkflowStepRuns.SingleAsync(s=>s.Order==1)).Status,Is.EqualTo(WorkflowDefinitionRunStatus.Completed));
        Assert.That(handler.Calls,Is.EqualTo(2));
        Assert.That(await observer.ContentArtifacts.CountAsync(),Is.EqualTo(2));
    }
    private sealed class RetryTransactionGate:DbTransactionInterceptor
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,TransactionStartingEventData eventData,InterceptionResult<DbTransaction> result,CancellationToken cancellationToken=default)
        {Started.TrySetResult();await Release.Task.WaitAsync(cancellationToken);return result;}
    }
    private sealed class RetryBlockingHandler:IFullWorkflowStepHandler
    {
        public string CapabilityKey=>"First";public int Calls;
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
        {Calls++;if(Calls==1)throw new InvalidOperationException("Fixture failure");Started.TrySetResult();await Release.Task.WaitAsync(cancellationToken);return await new Handler("First").ExecuteAsync(context,cancellationToken);}
    }
    private sealed class CancellingHandler(CancellationTokenSource cancellation):IFullWorkflowStepHandler
    {
        public string CapabilityKey=>"First";public int Calls;
        public Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
        {Calls++;if(Calls==1){cancellation.Cancel();cancellationToken.ThrowIfCancellationRequested();}return new Handler("First").ExecuteAsync(context,cancellationToken);}
    }
    private sealed class FailingSaveHandler(ContentOsDbContext db):IFullWorkflowStepHandler
    {
        public string CapabilityKey=>"First";
        public Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
        {db.IdeaRecords.Add(new(){Id=Guid.NewGuid(),IdeaTitle="Must roll back"});return Task.FromResult(new FullWorkflowStepResult(JsonSerializer.SerializeToElement(new{output="Actual fixture with a failed check"}),new Dictionary<string,StepAcceptanceEvidence>{{"SpecificEvidence",new(false,"Required evidence is missing",["fixture://failure"])}},true));}
    }
}
