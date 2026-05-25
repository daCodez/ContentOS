using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Workflow;

public sealed class WorkflowControlService : IWorkflowControlService
{
    private readonly ContentOsDbContext _dbContext;

    public WorkflowControlService(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowControlResultDto> ExecuteTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var (job, task) = await LoadTaskAsync(workflowJobId, taskId, cancellationToken);
        EnsureJobMutable(job.Status, allowPaused: true);

        if (task.Status == ContentOS.Domain.Enums.TaskStatus.Running || task.Status == ContentOS.Domain.Enums.TaskStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot execute a task that is already running.");
        }

        if (task.Status == ContentOS.Domain.Enums.TaskStatus.Rejected)
        {
            throw new InvalidOperationException("Rejected tasks require an explicit reset/rerun action.");
        }

        var oldStatus = task.Status.ToString();

        if (task.Status == ContentOS.Domain.Enums.TaskStatus.Completed)
        {
            task.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
            task.CompletedUtc = null;
        }
        else if (task.Status is not ContentOS.Domain.Enums.TaskStatus.Ready and not ContentOS.Domain.Enums.TaskStatus.Pending and not ContentOS.Domain.Enums.TaskStatus.Failed)
        {
            throw new InvalidOperationException($"Task {task.Name} cannot be executed from status {task.Status}.");
        }

        if (task.Status == ContentOS.Domain.Enums.TaskStatus.Pending)
        {
            task.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
        }

        task.ErrorMessage = string.Empty;
        task.LastUpdatedUtc = DateTime.UtcNow;
        task.ExecutionAttemptCount += 1;
        task.InputDataJson = MergeAuditMetadata(task.InputDataJson, request, "execute");
        job.Status = "InProgress";
        job.CurrentStage = task.StageName;
        job.LastUpdatedUtc = DateTime.UtcNow;
        job.CompletedUtc = null;

        await AddMutationLogAsync(job.Id, task.Id, WorkflowMutationType.ExecuteTask, new { status = oldStatus }, new { status = task.Status.ToString(), task.ExecutionAttemptCount }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(job.Id, task.Id, job.Status, task.Status.ToString(), $"Task '{task.Name}' queued for execution.");
    }

    public async Task<WorkflowControlResultDto> ResetTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var (job, task) = await LoadTaskAsync(workflowJobId, taskId, cancellationToken);
        EnsureJobMutable(job.Status, allowCompleted: true);

        var oldStatus = task.Status.ToString();
        task.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
        task.ErrorMessage = string.Empty;
        task.CompletedUtc = null;
        task.StartedUtc = null;
        task.OutputDataJson = "{}";
        task.ResultArtifactId = null;
        task.LastUpdatedUtc = DateTime.UtcNow;
        task.ExecutionAttemptCount += 1;
        task.InputDataJson = MergeAuditMetadata(task.InputDataJson, request, "reset");

        job.Status = "InProgress";
        job.CurrentStage = task.StageName;
        job.ErrorMessage = string.Empty;
        job.CompletedUtc = null;
        job.LastUpdatedUtc = DateTime.UtcNow;

        await AddMutationLogAsync(job.Id, task.Id, WorkflowMutationType.ResetTask, new { status = oldStatus }, new { status = task.Status.ToString(), task.ExecutionAttemptCount }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(job.Id, task.Id, job.Status, task.Status.ToString(), $"Task '{task.Name}' reset for rerun.");
    }

    public async Task<WorkflowControlResultDto> AcceptTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var (job, task) = await LoadTaskAsync(workflowJobId, taskId, cancellationToken);
        EnsureJobMutable(job.Status, allowCompleted: true);

        if (string.IsNullOrWhiteSpace(task.ResultArtifactId))
        {
            throw new InvalidOperationException("Cannot accept task without a produced result.");
        }

        if (task.Status != ContentOS.Domain.Enums.TaskStatus.Completed)
        {
            throw new InvalidOperationException("Only completed tasks can be accepted.");
        }

        task.InputDataJson = MergeAuditMetadata(task.InputDataJson, request, "accept");
        task.LastUpdatedUtc = DateTime.UtcNow;

        var nextTask = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == workflowJobId && x.DisplayOrder > task.DisplayOrder && x.Status == ContentOS.Domain.Enums.TaskStatus.Pending)
            .OrderBy(x => x.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextTask is not null)
        {
            nextTask.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
            nextTask.LastUpdatedUtc = DateTime.UtcNow;
            job.CurrentStage = nextTask.StageName;
            job.Status = "InProgress";
        }
        else
        {
            job.Status = "Completed";
            job.CompletedUtc = DateTime.UtcNow;
            job.CurrentStage = task.StageName;
        }

        job.LastUpdatedUtc = DateTime.UtcNow;
        await AddMutationLogAsync(job.Id, task.Id, WorkflowMutationType.AcceptTask, new { status = task.Status.ToString() }, new { job.Status, nextTask = nextTask?.Id }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(job.Id, task.Id, job.Status, task.Status.ToString(), $"Task '{task.Name}' accepted.");
    }

    public async Task<WorkflowControlResultDto> RejectTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var (job, task) = await LoadTaskAsync(workflowJobId, taskId, cancellationToken);
        EnsureJobMutable(job.Status, allowCompleted: true);

        if (task.Status is ContentOS.Domain.Enums.TaskStatus.Running or ContentOS.Domain.Enums.TaskStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot reject a task while it is running.");
        }

        var oldStatus = task.Status.ToString();
        task.Status = ContentOS.Domain.Enums.TaskStatus.Rejected;
        task.ErrorMessage = request.Reason ?? "Rejected for manual review.";
        task.LastUpdatedUtc = DateTime.UtcNow;
        task.InputDataJson = MergeAuditMetadata(task.InputDataJson, request, "reject");

        job.Status = "Blocked";
        job.CurrentStage = task.StageName;
        job.LastUpdatedUtc = DateTime.UtcNow;
        job.ErrorMessage = task.ErrorMessage;

        await AddMutationLogAsync(job.Id, task.Id, WorkflowMutationType.RejectTask, new { status = oldStatus }, new { job.Status, task.ErrorMessage }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(job.Id, task.Id, job.Status, task.Status.ToString(), $"Task '{task.Name}' rejected.");
    }

    public async Task<WorkflowControlResultDto> PauseWorkflowAsync(Guid workflowJobId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");

        EnsureJobMutable(job.Status);
        var oldStatus = job.Status;
        job.Status = "Paused";
        job.ErrorMessage = request.Reason ?? string.Empty;
        job.LastUpdatedUtc = DateTime.UtcNow;

        await AddMutationLogAsync(job.Id, null, WorkflowMutationType.PauseWorkflow, new { status = oldStatus }, new { job.Status }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return BuildResult(job.Id, null, job.Status, null, "Workflow paused.");
    }

    public async Task<WorkflowControlResultDto> ResumeWorkflowAsync(Guid workflowJobId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");

        if (string.Equals(job.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot resume cancelled workflow.");
        }

        if (!string.Equals(job.Status, "Paused", StringComparison.OrdinalIgnoreCase) && !string.Equals(job.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only paused or blocked workflows can be resumed.");
        }

        var oldStatus = job.Status;
        var nextTask = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == workflowJobId && x.Status == ContentOS.Domain.Enums.TaskStatus.Pending)
            .OrderBy(x => x.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextTask is null)
        {
            nextTask = await _dbContext.ContentWorkflowTasks
                .Where(x => x.ContentWorkflowJobId == workflowJobId && x.Status == ContentOS.Domain.Enums.TaskStatus.Rejected)
                .OrderBy(x => x.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextTask is not null)
            {
                nextTask.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
                nextTask.LastUpdatedUtc = DateTime.UtcNow;
            }
        }
        else
        {
            nextTask.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
            nextTask.LastUpdatedUtc = DateTime.UtcNow;
        }

        job.Status = "InProgress";
        job.CurrentStage = nextTask?.StageName ?? job.CurrentStage;
        job.ErrorMessage = request.Reason ?? string.Empty;
        job.LastUpdatedUtc = DateTime.UtcNow;

        await AddMutationLogAsync(job.Id, nextTask?.Id, WorkflowMutationType.ResumeWorkflow, new { status = oldStatus }, new { job.Status, nextTask = nextTask?.Id }, request, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return BuildResult(job.Id, nextTask?.Id, job.Status, nextTask?.Status.ToString(), "Workflow resumed.");
    }

    private async Task<(ContentWorkflowJob Job, ContentWorkflowTask Task)> LoadTaskAsync(Guid workflowJobId, Guid taskId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");
        var task = await _dbContext.ContentWorkflowTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.ContentWorkflowJobId == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow task not found.");
        return (job, task);
    }

    private static void EnsureJobMutable(string status, bool allowCompleted = false, bool allowPaused = false)
    {
        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cancelled workflows cannot be changed unless reopened.");
        }

        if (!allowCompleted && string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Completed workflows require an explicit reopen policy before mutation.");
        }

        if (!allowPaused && string.Equals(status, "Paused", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Paused workflows must be resumed before mutation.");
        }
    }

    private static string MergeAuditMetadata(string? existingJson, WorkflowTaskControlRequestDto request, string action)
    {
        Dictionary<string, object?> payload;
        if (!string.IsNullOrWhiteSpace(existingJson) && existingJson != "{}")
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingJson) ?? new Dictionary<string, object?>();
        }
        else
        {
            payload = new Dictionary<string, object?>();
        }

        payload[$"{action}Meta"] = new
        {
            request.Reason,
            request.CreatedBy,
            request.Source,
            request.CorrelationId,
            recordedUtc = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task AddMutationLogAsync(Guid workflowJobId, Guid? taskId, string mutationType, object oldValues, object newValues, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        await _dbContext.WorkflowMutationLogs.AddAsync(new WorkflowMutationLog
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = workflowJobId,
            ContentWorkflowTaskId = taskId,
            MutationType = mutationType,
            OldValuesJson = JsonSerializer.Serialize(oldValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            PreviousStatus = oldValues.GetType().GetProperty("status")?.GetValue(oldValues)?.ToString() ?? string.Empty,
            NewStatus = newValues.GetType().GetProperty("status")?.GetValue(newValues)?.ToString() ?? string.Empty,
            ActorIdentity = request.CreatedBy ?? "unknown",
            ActorType = request.ActorType,
            Reason = request.Reason ?? string.Empty,
            Source = request.Source,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private static WorkflowControlResultDto BuildResult(Guid workflowJobId, Guid? taskId, string jobStatus, string? taskStatus, string message)
        => new()
        {
            WorkflowJobId = workflowJobId,
            TaskId = taskId,
            JobStatus = jobStatus,
            TaskStatus = taskStatus,
            Message = message
        };
}
