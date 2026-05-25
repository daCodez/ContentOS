using System.Text.Json;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Workflow;

public interface INewWorkflowRuntimeEngine
{
    Task ProcessPendingRunsAsync(CancellationToken cancellationToken = default);
}

public class NewWorkflowRuntimeEngine : INewWorkflowRuntimeEngine
{
    private readonly ContentOsDbContext _dbContext;
    private readonly NewWorkflowRuntimeDispatcher _dispatcher;
    private readonly ILogger<NewWorkflowRuntimeEngine> _logger;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public NewWorkflowRuntimeEngine(ContentOsDbContext dbContext, NewWorkflowRuntimeDispatcher dispatcher, ILogger<NewWorkflowRuntimeEngine> logger)
    {
        _dbContext = dbContext;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ProcessPendingRunsAsync(CancellationToken cancellationToken = default)
    {
        if (!await _lock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return;
        }

        try
        {
            try
            {
                await RunQueueHygieneAsync(cancellationToken);

                while (true)
                {
                    var candidate = await GetNextRunnableCandidateAsync(cancellationToken);
                    if (candidate is null)
                    {
                        return;
                    }

                    var processed = await TryProcessCandidateAsync(candidate, cancellationToken);
                    if (processed)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // If we're cancelled while processing, just exit gracefully
                return;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ProcessPendingRunsOnceForDebugAsync(CancellationToken cancellationToken = default)
    {
        if (!await _lock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return "busy";
        }

        try
        {
            _logger.LogInformation("[DEBUG] Starting debug run check. Checking raw table counts...");
            var readyCount = await _dbContext.WorkflowActionRuns.CountAsync(x => x.Status == WorkflowDefinitionRunStatus.Ready, cancellationToken);
            _logger.LogInformation("[DEBUG] Raw count of Ready actions in DB: {Count}", readyCount);

            await RunQueueHygieneAsync(cancellationToken);

            while (true)
            {
                var candidate = await GetNextRunnableCandidateAsync(cancellationToken);
                if (candidate is null)
                {
                    _logger.LogInformation("[DEBUG] GetNextRunnableCandidateAsync returned null.");
                    return "no-runnable-candidates";
                }

                var processed = await TryProcessCandidateAsync(candidate, cancellationToken);
                if (processed)
                {
                    return $"processed:{candidate.ActionRun.Id}";
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Guid> SeedCleanTestRunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SEED] Starting clean test run seeding...");
        
        var family = await _dbContext.WorkflowDefinitionFamilies
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.WorkflowType == WorkflowDefinitionType.Article, cancellationToken)
            ?? throw new InvalidOperationException("No Article workflow family found.");

        var definition = await _dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.Id == family.ActiveWorkflowDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("No active article definition found.");

        var idea = await _dbContext.IdeaRecords
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(x => x.Status == IdeaRecordStatus.Approved, cancellationToken)
            ?? throw new InvalidOperationException("No approved IdeaRecord found.");

        var runId = Guid.NewGuid();
        var run = new WorkflowDefinitionRun
        {
            Id = runId,
            WorkflowDefinitionFamilyId = family.Id,
            WorkflowDefinitionId = definition.Id,
            WorkflowType = definition.WorkflowType,
            Version = definition.Version,
            Status = WorkflowDefinitionRunStatus.Ready,
            StartedUtc = DateTime.UtcNow,
            TriggeredBy = "InternalDebugSeed",
            InputSnapshotJson = "{}",
            IdeaRecordId = idea.Id
        };

        _dbContext.WorkflowDefinitionRuns.Add(run);

        var actions = await _dbContext.WorkflowActionDefinitions
            .Where(x => x.WorkflowDefinitionId == definition.Id && x.IsEnabled)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var actionRun = new WorkflowActionRun
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionRunId = run.Id,
                WorkflowActionDefinitionId = action.Id,
                Name = action.Name,
                Order = action.Order,
                Status = i == 0 ? WorkflowDefinitionRunStatus.Ready : WorkflowDefinitionRunStatus.Pending,
                RetryCount = 0,
                MaxRetry = 3,
                InputSnapshotJson = "{}"
            };
            _dbContext.WorkflowActionRuns.Add(actionRun);

            var steps = await _dbContext.WorkflowStepDefinitions
                .Where(x => x.WorkflowActionDefinitionId == action.Id && x.IsEnabled)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            foreach (var step in steps)
            {
                _dbContext.WorkflowStepRuns.Add(new WorkflowStepRun
                {
                    Id = Guid.NewGuid(),
                    WorkflowActionRunId = actionRun.Id,
                    WorkflowStepDefinitionId = step.Id,
                    Name = step.Name,
                    Order = step.Order,
                    Status = (i == 0 && step.Order == 1) ? WorkflowDefinitionRunStatus.Ready : WorkflowDefinitionRunStatus.Pending,
                    InputSnapshotJson = "{}"
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[SEED] Successfully seeded run {RunId} with {ActionCount} actions.", runId, actions.Count);
        return runId;
    }

    private async Task<NewWorkflowRunnableCandidate?> GetNextRunnableCandidateAsync(CancellationToken cancellationToken)
    {
        var actionRun = await _dbContext.WorkflowActionRuns
            .Where(action => action.Status == WorkflowDefinitionRunStatus.Ready)
            .OrderBy(action => action.StartedUtc ?? DateTime.MinValue)
            .ThenBy(action => action.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (actionRun is null)
        {
            return null;
        }

        _logger.LogInformation("[CANDIDATE] Checking action run {ActionRunId} for readiness.", actionRun.Id);

        var workflowRun = await _dbContext.WorkflowDefinitionRuns
            .FirstOrDefaultAsync(run => run.Id == actionRun.WorkflowDefinitionRunId, cancellationToken);

        if (workflowRun is null)
        {
            _logger.LogWarning("[CANDIDATE] Action run {ActionRunId} has missing parent workflow run {WorkflowRunId}.", actionRun.Id, actionRun.WorkflowDefinitionRunId);
            return null;
        }

        if (workflowRun.WorkflowType != WorkflowDefinitionType.Article)
        {
            _logger.LogInformation("[CANDIDATE] Action run {ActionRunId} skipped: parent workflow is not Article type.", actionRun.Id);
            return null;
        }

        var definition = await _dbContext.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == workflowRun.WorkflowDefinitionId, cancellationToken);

        if (definition is null)
        {
            _logger.LogWarning("[CANDIDATE] Action run {ActionRunId} has missing definition {DefinitionId}.", actionRun.Id, workflowRun.WorkflowDefinitionId);
            return null;
        }

        var actionDefinition = await _dbContext.WorkflowActionDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(ad => ad.Id == actionRun.WorkflowActionDefinitionId, cancellationToken);

        if (actionDefinition is null)
        {
            _logger.LogWarning("[CANDIDATE] Action run {ActionRunId} has missing action definition {ActionDefinitionId}.", actionRun.Id, actionRun.WorkflowActionDefinitionId);
            return null;
        }

        return new NewWorkflowRunnableCandidate(actionRun, workflowRun, actionDefinition, definition);
    }

    private async Task<bool> TryProcessCandidateAsync(NewWorkflowRunnableCandidate candidate, CancellationToken cancellationToken)
    {
        var actionRun = candidate.ActionRun;
        var workflowRun = candidate.WorkflowRun;
        var actionDefinition = candidate.ActionDefinition;
        var definition = candidate.WorkflowDefinition;

        _logger.LogInformation("Candidate selected {ActionRunId} for workflow run {WorkflowRunId}, action {ActionName}.", actionRun.Id, workflowRun.Id, actionRun.Name);

        if (workflowRun.Status == WorkflowDefinitionRunStatus.Completed || workflowRun.Status == WorkflowDefinitionRunStatus.Failed)
        {
            await FailPoisonedActionAsync(actionRun, "Parent workflow run not runnable", cancellationToken);
            return false;
        }

        if (workflowRun.Version != definition.Version)
        {
            await FailPoisonedActionAsync(actionRun, $"Workflow version mismatch. Run expects version {workflowRun.Version} but definition is version {definition.Version}.", cancellationToken);
            return false;
        }

        var requiredPriorActionIncomplete = await _dbContext.WorkflowActionRuns
            .AnyAsync(x => x.WorkflowDefinitionRunId == workflowRun.Id && x.Order < actionRun.Order && x.Status != WorkflowDefinitionRunStatus.Completed, cancellationToken);
        if (requiredPriorActionIncomplete)
        {
            await FailPoisonedActionAsync(actionRun, "Required prior action not completed", cancellationToken);
            return false;
        }

        _logger.LogInformation("Parent run found {WorkflowRunId}.", workflowRun.Id);
        _logger.LogInformation("Action definition found {ActionDefinitionId}.", actionDefinition.Id);
        _logger.LogInformation("Workflow definition found {WorkflowDefinitionId} version {Version}.", definition.Id, definition.Version);

        actionRun.Status = WorkflowDefinitionRunStatus.InProgress;
        actionRun.StartedUtc ??= DateTime.UtcNow;
        workflowRun.Status = WorkflowDefinitionRunStatus.InProgress;
        actionRun.InputSnapshotJson = actionRun.InputSnapshotJson ?? workflowRun.InputSnapshotJson;
        _logger.LogInformation("Candidate marked InProgress {ActionRunId}.", actionRun.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Agent dispatch started for {ActionRunId} using agent {Agent}.", actionRun.Id, actionDefinition.AssignedAgent);
            var dispatch = await _dispatcher.DispatchAsync(workflowRun, actionRun, actionDefinition, cancellationToken);

            actionRun.OutputSnapshotJson = JsonSerializer.Serialize(dispatch.Payload);
            actionRun.Status = WorkflowDefinitionRunStatus.Completed;
            actionRun.CompletedUtc = DateTime.UtcNow;
            await MarkStepRunsCompletedAsync(actionRun.Id, cancellationToken);
            _logger.LogInformation("Output persisted for {ActionRunId}.", actionRun.Id);

            var nextAction = await _dbContext.WorkflowActionRuns
                .Where(x => x.WorkflowDefinitionRunId == workflowRun.Id && x.Order > actionRun.Order)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextAction is null)
            {
                workflowRun.Status = WorkflowDefinitionRunStatus.Completed;
                workflowRun.CompletedUtc = DateTime.UtcNow;
            }
            else
            {
                nextAction.Status = WorkflowDefinitionRunStatus.Ready;
                _logger.LogInformation("Next action promoted {NextActionRunId} for workflow run {WorkflowRunId}.", nextAction.Id, workflowRun.Id);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            actionRun.RetryCount += 1;
            actionRun.ErrorMessage = ex.Message;
            actionRun.LastFailureUtc = DateTime.UtcNow;

            if (actionRun.RetryCount < actionRun.MaxRetry)
            {
                actionRun.Status = WorkflowDefinitionRunStatus.Ready;
            }
            else
            {
                actionRun.Status = WorkflowDefinitionRunStatus.Failed;
                workflowRun.Status = WorkflowDefinitionRunStatus.Failed;
                workflowRun.ErrorMessage = ex.Message;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    private async Task FailPoisonedActionAsync(WorkflowActionRun actionRun, string reason, CancellationToken cancellationToken)
    {
        actionRun.Status = WorkflowDefinitionRunStatus.Failed;
        actionRun.ErrorMessage = reason;
        actionRun.LastFailureUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RunQueueHygieneAsync(CancellationToken cancellationToken)
    {
        var orphanedReadyActions = await _dbContext.WorkflowActionRuns
            .Where(action => action.Status == WorkflowDefinitionRunStatus.Ready)
            .GroupJoin(_dbContext.WorkflowDefinitionRuns,
                action => action.WorkflowDefinitionRunId,
                run => run.Id,
                (action, runs) => new { action, hasRun = runs.Any() })
            .Where(x => !x.hasRun)
            .Select(x => x.action)
            .ToListAsync(cancellationToken);

        foreach (var actionRun in orphanedReadyActions)
        {
            actionRun.Status = WorkflowDefinitionRunStatus.Failed;
            actionRun.ErrorMessage = "Orphaned ready action run";
            actionRun.LastFailureUtc = DateTime.UtcNow;
        }

        if (orphanedReadyActions.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task MarkStepRunsCompletedAsync(Guid actionRunId, CancellationToken cancellationToken)
    {
        var stepRuns = await _dbContext.WorkflowStepRuns.Where(x => x.WorkflowActionRunId == actionRunId).ToListAsync(cancellationToken);
        foreach (var stepRun in stepRuns)
        {
            stepRun.Status = WorkflowDefinitionRunStatus.Completed;
            stepRun.StartedUtc ??= DateTime.UtcNow;
            stepRun.CompletedUtc = DateTime.UtcNow;
        }
    }

    private sealed record NewWorkflowRunnableCandidate(
        WorkflowActionRun ActionRun,
        WorkflowDefinitionRun WorkflowRun,
        WorkflowActionDefinition ActionDefinition,
        WorkflowDefinition WorkflowDefinition);
}
