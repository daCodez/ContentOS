using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContentOS.Infrastructure.Workflow;

public sealed record StepAcceptanceEvidence(bool Passed,string Explanation,IReadOnlyList<string> EvidenceReferences);
public sealed record FullWorkflowStepResult(JsonElement Output,IReadOnlyDictionary<string,StepAcceptanceEvidence> Checks,bool IsFixture=false);
public sealed record FullWorkflowStepContext(Guid RunId,Guid StepRunId,FullWorkflowSpecification Specification,FullSpecificationStep Step,
    JsonElement FrozenRunInput,JsonElement? PreviousOutput,string InputHash,int Attempt,bool FixtureMode);
public interface IFullWorkflowStepHandler
{
    string CapabilityKey { get; }
    Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken);
}

/// <summary>Executes and persists each actual configured step; failed-step retries preserve completed outputs.</summary>
public sealed class FullWorkflowStepRuntime(ContentOsDbContext db,FullWorkflowSpecificationStore store,IEnumerable<IFullWorkflowStepHandler> handlers,IFullWorkflowProductionAuthorization? authorization=null)
{
    private readonly Dictionary<string,IFullWorkflowStepHandler> _handlers=handlers.ToDictionary(h=>h.CapabilityKey,StringComparer.Ordinal);
    public Task<WorkflowDefinitionRun> StartFixtureAsync(Guid definitionId,string inputJson,CancellationToken cancellationToken)
        =>StartAsync(definitionId,inputJson,true,cancellationToken);
    public Task<WorkflowDefinitionRun> StartProductionAsync(Guid definitionId,string inputJson,CancellationToken cancellationToken)
        =>StartAsync(definitionId,inputJson,false,cancellationToken);

    private async Task<WorkflowDefinitionRun> StartAsync(Guid definitionId,string inputJson,bool fixture,CancellationToken cancellationToken)
    {
        var definition=await db.WorkflowDefinitions.SingleAsync(d=>d.Id==definitionId,cancellationToken);
        var specJson=await store.GetSourceJsonAsync(definitionId,cancellationToken);var spec=FullWorkflowSpecification.Parse(specJson);
        using var input=JsonDocument.Parse(inputJson);if(input.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Workflow input must be an object.");
        if(!fixture)
        {
            if(!definition.IsActive)throw new InvalidOperationException("Production execution requires an explicitly active full specification.");
            FullWorkflowProductionContract.Validate(spec,authorization);
            if(spec.WorkflowType==WorkflowDefinitionType.Article)authorization!.ValidateExecution(spec,input.RootElement,null);
            var missing=spec.Actions.SelectMany(a=>a.Steps).Select(s=>s.CapabilityKey).Where(k=>!_handlers.ContainsKey(k)).ToArray();
            if(missing.Length>0)throw new InvalidOperationException("Unsupported production steps: "+string.Join(", ",missing));
            if(spec.WorkflowType==WorkflowDefinitionType.Article)
            {
                if(!input.RootElement.TryGetProperty("ideaId",out var ideaId)||!Guid.TryParse(ideaId.GetString(),out var id))throw new InvalidOperationException("An approved idea ID is required.");
                var idea=await db.IdeaRecords.SingleOrDefaultAsync(i=>i.Id==id,cancellationToken);
                if(idea?.Status!=IdeaRecordStatus.Approved||idea.ApprovedUtc is null||string.IsNullOrWhiteSpace(idea.ApprovedBy))throw new InvalidOperationException("Article blocked: approved idea record is missing.");
            }
        }
        var run=new WorkflowDefinitionRun{Id=Guid.NewGuid(),WorkflowDefinitionId=definition.Id,WorkflowDefinitionFamilyId=definition.WorkflowDefinitionFamilyId,WorkflowType=definition.WorkflowType,Version=definition.Version,Status=WorkflowDefinitionRunStatus.InProgress,TriggeredBy=fixture?"OfflineFixture":"ExplicitRevisedWorkflowRun",InputSnapshotJson=JsonSerializer.Serialize(new{fixtureMode=fixture,frozenInput=input.RootElement,specificationHash=Hash(specJson)})};
        db.WorkflowDefinitionRuns.Add(run);
        var actions=await db.WorkflowActionDefinitions.Where(a=>a.WorkflowDefinitionId==definitionId).OrderBy(a=>a.Order).ToListAsync(cancellationToken);
        foreach(var action in actions)
        {
            var actionRun=new WorkflowActionRun{Id=Guid.NewGuid(),WorkflowDefinitionRunId=run.Id,WorkflowActionDefinitionId=action.Id,Name=action.Name,Order=action.Order,Status=WorkflowDefinitionRunStatus.Pending,MaxRetry=MaxRetries(spec)};
            db.WorkflowActionRuns.Add(actionRun);
            var steps=await db.WorkflowStepDefinitions.Where(s=>s.WorkflowActionDefinitionId==action.Id).OrderBy(s=>s.Order).ToListAsync(cancellationToken);
            foreach(var step in steps)db.WorkflowStepRuns.Add(new(){Id=Guid.NewGuid(),WorkflowActionRunId=actionRun.Id,WorkflowStepDefinitionId=step.Id,Name=step.Name,Order=step.Order,Status=WorkflowDefinitionRunStatus.Pending});
        }
        await db.SaveChangesAsync(cancellationToken);return run;
    }

    public async Task ProcessAsync(Guid runId,CancellationToken cancellationToken)
    {
        var run=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==runId,cancellationToken);
        if(run.Status!=WorkflowDefinitionRunStatus.InProgress)return;
        var json=await store.GetSourceJsonAsync(run.WorkflowDefinitionId,cancellationToken);var spec=FullWorkflowSpecification.Parse(json);
        using var frozen=JsonDocument.Parse(run.InputSnapshotJson);
        if(frozen.RootElement.GetProperty("specificationHash").GetString()!=Hash(json))throw new InvalidOperationException("Frozen workflow specification changed; execution blocked.");
        var fixture=frozen.RootElement.GetProperty("fixtureMode").GetBoolean();var input=frozen.RootElement.GetProperty("frozenInput").Clone();
        if(!fixture){FullWorkflowProductionContract.Validate(spec,authorization);if(spec.WorkflowType==WorkflowDefinitionType.Article)authorization!.ValidateExecution(spec,input,run.Id);}
        JsonElement? previous=null;var fixtureOutput=false;
        var actionRuns=await db.WorkflowActionRuns.Where(a=>a.WorkflowDefinitionRunId==run.Id).OrderBy(a=>a.Order).ToListAsync(cancellationToken);
        foreach(var actionRun in actionRuns)
        {
            var actionDefinition=await db.WorkflowActionDefinitions.SingleAsync(a=>a.Id==actionRun.WorkflowActionDefinitionId,cancellationToken);
            var sourceAction=spec.Actions.Single(a=>a.Order==actionDefinition.Order);
            var steps=await db.WorkflowStepRuns.Where(s=>s.WorkflowActionRunId==actionRun.Id).OrderBy(s=>s.Order).ToListAsync(cancellationToken);
            actionRun.Status=WorkflowDefinitionRunStatus.InProgress;actionRun.StartedUtc??=DateTime.UtcNow;
            foreach(var stepRun in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceStep=sourceAction.Steps.Single(s=>s.Order==stepRun.Order);
                var inputHash=Hash(JsonSerializer.Serialize(new{input,previous,specificationHash=Hash(json),sourceStep.SourceId}));
                if(stepRun.Status==WorkflowDefinitionRunStatus.Completed)
                {
                    using var saved=JsonDocument.Parse(stepRun.OutputSnapshotJson??throw new InvalidOperationException("Completed step lacks its actual output."));
                    if(saved.RootElement.GetProperty("inputHash").GetString()!=inputHash)throw new InvalidOperationException("Completed step input version differs; stale output cannot be reused.");
                    var output=saved.RootElement.GetProperty("payload");
                    if(saved.RootElement.GetProperty("outputHash").GetString()!=Hash(JsonSerializer.Serialize(output)))throw new InvalidOperationException("Persisted step output hash differs; execution blocked.");
                    fixtureOutput|=saved.RootElement.GetProperty("isFixture").GetBoolean();previous=output.Clone();continue;
                }
                if(stepRun.Status is WorkflowDefinitionRunStatus.Failed or WorkflowDefinitionRunStatus.InProgress)return; // Explicit failed/interrupted recovery is required.
                var attempt=Attempt(stepRun)+1;
                var startedUtc=stepRun.StartedUtc??DateTime.UtcNow;
                var inputSnapshot=JsonSerializer.Serialize(new{inputHash,attempt,frozenInput=input,previousOutput=previous});
                var claimed=await db.WorkflowStepRuns.Where(s=>s.Id==stepRun.Id&&s.Status==WorkflowDefinitionRunStatus.Pending)
                    .ExecuteUpdateAsync(update=>update.SetProperty(s=>s.Status,WorkflowDefinitionRunStatus.InProgress)
                        .SetProperty(s=>s.StartedUtc,startedUtc).SetProperty(s=>s.InputSnapshotJson,inputSnapshot),cancellationToken);
                if(claimed!=1)return; // Another executor owns this step; do not repeat external work.
                stepRun.Status=WorkflowDefinitionRunStatus.InProgress;stepRun.StartedUtc=startedUtc;stepRun.InputSnapshotJson=inputSnapshot;
                await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
                FullWorkflowStepResult? attemptedResult=null;
                try
                {
                    if(!_handlers.TryGetValue(sourceStep.CapabilityKey,out var handler))throw new InvalidOperationException("Unsupported step: "+sourceStep.CapabilityKey);
                    var result=await handler.ExecuteAsync(new(run.Id,stepRun.Id,spec,sourceStep,input,previous,inputHash,attempt,fixture),cancellationToken);
                    attemptedResult=result;
                    cancellationToken.ThrowIfCancellationRequested();
                    if(result.Output.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined||result.Output.GetRawText() is "{}" or "[]")throw new InvalidOperationException("Step did not return actual output.");
                    if(result.IsFixture&&!fixture)throw new InvalidOperationException("Fixture output cannot satisfy production execution.");
                    var checks=new Dictionary<string,StepAcceptanceEvidence>(result.Checks,StringComparer.Ordinal)
                    {
                        ["OutputPresent"]=new(true,"Actual handler output returned",["step://"+stepRun.Id]),
                        ["InputVersionMatched"]=new(true,"Handler received the frozen input and upstream output hash",["sha256:"+inputHash]),
                        ["NotSimulated"]=new(!result.IsFixture,"Fixture output remains explicitly flagged and cannot satisfy production delivery",["step://"+stepRun.Id]),
                        ["OutputArtifactPersisted"]=new(true,"Output and acceptance evidence are saved atomically with the completed step",["step://"+stepRun.Id])
                    };
                    foreach(var required in sourceStep.AcceptanceCheckKeys)
                    {
                        if(!checks.TryGetValue(required,out var evidence)||(!evidence.Passed&&!(fixture&&required=="NotSimulated"))||string.IsNullOrWhiteSpace(evidence.Explanation)||evidence.EvidenceReferences.Count==0)
                            throw new InvalidOperationException("Acceptance check failed or lacks evidence: "+required);
                    }
                    var outputHash=Hash(JsonSerializer.Serialize(result.Output));
                    var snapshot=JsonSerializer.Serialize(new{payload=result.Output,acceptanceChecks=checks,inputHash,outputHash,isFixture=result.IsFixture,sourceStepId=sourceStep.SourceId,attempt});
                    stepRun.OutputSnapshotJson=snapshot;stepRun.Status=WorkflowDefinitionRunStatus.Completed;stepRun.CompletedUtc=DateTime.UtcNow;stepRun.ErrorMessage=null;
                    db.ContentArtifacts.Add(new(){Id=Guid.NewGuid(),ContentWorkflowJobId=run.Id,ArtifactType="WorkflowStepEvidence",Title=stepRun.Name,StorageType="Database",ContentJson=snapshot,ContentText=result.Output.GetRawText(),CreatedByAgent=sourceStep.CapabilityKey,VersionNumber=attempt});
                    await db.SaveChangesAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);previous=result.Output.Clone();fixtureOutput|=result.IsFixture;
                }
                catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested)
                {
                    await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
                    await MarkInterruptedAsync(run.Id,actionRun.Id,stepRun.Id,CancellationToken.None);throw;
                }
                catch(Exception ex)
                {
                    await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
                    var failedStep=await db.WorkflowStepRuns.SingleAsync(s=>s.Id==stepRun.Id,cancellationToken);
                    var failedAction=await db.WorkflowActionRuns.SingleAsync(a=>a.Id==actionRun.Id,cancellationToken);
                    var failedRun=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==run.Id,cancellationToken);
                    failedStep.Status=WorkflowDefinitionRunStatus.Failed;failedStep.ErrorMessage=ex is InvalidOperationException?ex.Message:"Step provider failed; inspect safe provider diagnostics.";
                    failedAction.Status=WorkflowDefinitionRunStatus.Failed;failedAction.ErrorMessage=failedStep.ErrorMessage;failedAction.LastFailureUtc=DateTime.UtcNow;
                    failedRun.Status=WorkflowDefinitionRunStatus.Failed;failedRun.ErrorMessage=failedStep.ErrorMessage;
                    if(attemptedResult is not null)
                    {
                        var failedSnapshot=JsonSerializer.Serialize(new{payload=attemptedResult.Output,acceptanceChecks=attemptedResult.Checks,inputHash,outputHash=Hash(JsonSerializer.Serialize(attemptedResult.Output)),isFixture=attemptedResult.IsFixture,sourceStepId=sourceStep.SourceId,attempt,accepted=false});
                        failedStep.OutputSnapshotJson=failedSnapshot;
                        db.ContentArtifacts.Add(new(){Id=Guid.NewGuid(),ContentWorkflowJobId=run.Id,ArtifactType="WorkflowStepFailedEvidence",Title=stepRun.Name,StorageType="Database",ContentJson=failedSnapshot,ContentText=attemptedResult.Output.GetRawText(),CreatedByAgent=sourceStep.CapabilityKey,VersionNumber=attempt});
                    }
                    run.Status=failedRun.Status;run.ErrorMessage=failedRun.ErrorMessage;
                    await db.SaveChangesAsync(cancellationToken);return;
                }
            }
            actionRun.Status=WorkflowDefinitionRunStatus.Completed;actionRun.CompletedUtc=DateTime.UtcNow;
            actionRun.OutputSnapshotJson=JsonSerializer.Serialize(new{stepRunIds=steps.Select(s=>s.Id),finalStepOutput=previous});
            await db.SaveChangesAsync(cancellationToken);
        }
        // Scores never replace explicit human approval or verified delivery dependencies.
        run.Status=WorkflowDefinitionRunStatus.AwaitingHumanApproval;run.CompletedUtc=null;
        run.ErrorMessage=fixture||fixtureOutput?"Fixture execution is not production-ready; explicit human approval and verified delivery are required.":"Execution outputs are ready for review; explicit human approval and verified delivery are required.";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryFailedStepAsync(Guid runId,CancellationToken cancellationToken)
    {
        var run=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==runId,cancellationToken);
        if(run.Status!=WorkflowDefinitionRunStatus.Failed)throw new InvalidOperationException("Only a failed workflow may be resumed.");
        var spec=FullWorkflowSpecification.Parse(await store.GetSourceJsonAsync(run.WorkflowDefinitionId,cancellationToken));
        ValidateSavedAuthorization(run,spec);
        var actions=await db.WorkflowActionRuns.Where(a=>a.WorkflowDefinitionRunId==runId).ToListAsync(cancellationToken);var ids=actions.Select(a=>a.Id).ToArray();
        var step=await db.WorkflowStepRuns.SingleOrDefaultAsync(s=>ids.Contains(s.WorkflowActionRunId)&&s.Status==WorkflowDefinitionRunStatus.Failed,cancellationToken)??throw new InvalidOperationException("Exactly one failed step is required.");
        if(Attempt(step)>=1+MaxRetries(spec))throw new InvalidOperationException("Failed-step retry limit reached.");
        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        var reset=await db.WorkflowStepRuns.Where(s=>s.Id==step.Id&&s.Status==WorkflowDefinitionRunStatus.Failed&&s.InputSnapshotJson==step.InputSnapshotJson)
            .ExecuteUpdateAsync(update=>update.SetProperty(s=>s.Status,WorkflowDefinitionRunStatus.Pending).SetProperty(s=>s.ErrorMessage,(string?)null).SetProperty(s=>s.OutputSnapshotJson,(string?)null),cancellationToken);
        if(reset!=1)throw new InvalidOperationException("Failed step changed or another caller already resumed it; no retry was scheduled.");
        await db.WorkflowActionRuns.Where(a=>a.Id==step.WorkflowActionRunId)
            .ExecuteUpdateAsync(update=>update.SetProperty(a=>a.Status,WorkflowDefinitionRunStatus.InProgress).SetProperty(a=>a.ErrorMessage,(string?)null).SetProperty(a=>a.RetryCount,a=>a.RetryCount+1),cancellationToken);
        await db.WorkflowDefinitionRuns.Where(r=>r.Id==runId&&r.Status==WorkflowDefinitionRunStatus.Failed)
            .ExecuteUpdateAsync(update=>update.SetProperty(r=>r.Status,WorkflowDefinitionRunStatus.InProgress).SetProperty(r=>r.ErrorMessage,(string?)null),cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Do not let a context's pre-retry tracked snapshots overwrite the atomic transitions.
        await db.Entry(step).ReloadAsync(cancellationToken);await db.Entry(run).ReloadAsync(cancellationToken);
        await db.Entry(actions.Single(a=>a.Id==step.WorkflowActionRunId)).ReloadAsync(cancellationToken);
    }
    public async Task RecoverInterruptedStepAsync(Guid runId,bool confirmedExecutorStopped,CancellationToken cancellationToken)
    {
        if(!confirmedExecutorStopped)throw new InvalidOperationException("Interrupted recovery requires explicit confirmation that the previous executor has stopped; no step state was changed.");
        var run=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==runId,cancellationToken);
        ValidateSavedAuthorization(run,FullWorkflowSpecification.Parse(await store.GetSourceJsonAsync(run.WorkflowDefinitionId,cancellationToken)));
        var actionIds=await db.WorkflowActionRuns.Where(a=>a.WorkflowDefinitionRunId==runId).Select(a=>a.Id).ToArrayAsync(cancellationToken);
        var step=await db.WorkflowStepRuns.SingleOrDefaultAsync(s=>actionIds.Contains(s.WorkflowActionRunId)&&s.Status==WorkflowDefinitionRunStatus.InProgress,cancellationToken)
            ??throw new InvalidOperationException("Exactly one interrupted claimed step is required; verify no executor is still running before recovery.");
        await MarkInterruptedAsync(run.Id,step.WorkflowActionRunId,step.Id,cancellationToken);
        // Recovery marks failure only. A separate explicit RetryFailedStepAsync is required to invoke the operation again.
    }
    private async Task MarkInterruptedAsync(Guid runId,Guid actionId,Guid stepId,CancellationToken cancellationToken)
    {
        var step=await db.WorkflowStepRuns.SingleAsync(s=>s.Id==stepId,cancellationToken);
        var action=await db.WorkflowActionRuns.SingleAsync(a=>a.Id==actionId,cancellationToken);
        var run=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==runId,cancellationToken);
        const string reason="Interrupted step: external operation outcome may be unknown. Review provider/output state before explicitly retrying this step.";
        step.Status=WorkflowDefinitionRunStatus.Failed;step.ErrorMessage=reason;action.Status=WorkflowDefinitionRunStatus.Failed;action.ErrorMessage=reason;run.Status=WorkflowDefinitionRunStatus.Failed;run.ErrorMessage=reason;
        await db.SaveChangesAsync(cancellationToken);
    }
    private void ValidateSavedAuthorization(WorkflowDefinitionRun run,FullWorkflowSpecification spec)
    {
        using var frozen=JsonDocument.Parse(run.InputSnapshotJson);
        if(!frozen.RootElement.GetProperty("fixtureMode").GetBoolean())
        {FullWorkflowProductionContract.Validate(spec,authorization);if(spec.WorkflowType==WorkflowDefinitionType.Article)authorization!.ValidateExecution(spec,frozen.RootElement.GetProperty("frozenInput"),run.Id);}
    }
    private static int Attempt(WorkflowStepRun step){if(string.IsNullOrWhiteSpace(step.InputSnapshotJson))return 0;using var document=JsonDocument.Parse(step.InputSnapshotJson);return document.RootElement.TryGetProperty("attempt",out var attempt)?attempt.GetInt32():0;}
    private static int MaxRetries(FullWorkflowSpecification spec)=>spec.Root.TryGetProperty("FailureHandling",out var failure)&&failure.TryGetProperty("MaxRetries",out var retries)?Math.Clamp(retries.GetInt32(),0,2):2;
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
