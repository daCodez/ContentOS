using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Workflow;

public sealed class WorkflowMutationService : IWorkflowMutationService
{
    private readonly ContentOsDbContext _dbContext;

    public WorkflowMutationService(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowTaskMutationResultDto> InjectManualTaskAsync(Guid workflowJobId, InjectWorkflowTaskRequestDto request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs
            .FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");

        EnsureMutable(job.Status, allowCompleted: request.InsertAfterTaskId.HasValue);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == workflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (tasks.Count == 0)
        {
            throw new InvalidOperationException("Workflow has no tasks to insert around.");
        }

        var scopeType = Enum.TryParse<TargetScopeType>(request.ScopeType, true, out var parsedScopeType)
            ? parsedScopeType
            : TargetScopeType.Global;

        var insertAfterTask = request.InsertAfterTaskId.HasValue
            ? tasks.FirstOrDefault(x => x.Id == request.InsertAfterTaskId.Value)
            : tasks.Last();

        if (request.InsertAfterTaskId.HasValue && insertAfterTask is null)
        {
            throw new InvalidOperationException("Insert-after task was not found.");
        }

        if (insertAfterTask is not null && insertAfterTask.Status is ContentOS.Domain.Enums.TaskStatus.Running or ContentOS.Domain.Enums.TaskStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot inject after a running task.");
        }

        var insertionOrder = (insertAfterTask?.DisplayOrder ?? 0) + 1;
        foreach (var task in tasks.Where(x => x.DisplayOrder >= insertionOrder))
        {
            task.DisplayOrder += 1;
            task.LastUpdatedUtc = DateTime.UtcNow;
        }

        var previousTask = tasks
            .Where(x => x.DisplayOrder < insertionOrder)
            .OrderByDescending(x => x.DisplayOrder)
            .FirstOrDefault();

        var manualTask = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = workflowJobId,
            Name = request.Name,
            StageName = string.IsNullOrWhiteSpace(request.StageName) ? "Manual" : request.StageName,
            DisplayOrder = insertionOrder,
            AssignedAgent = string.IsNullOrWhiteSpace(request.AssignedAgent) ? "manual" : request.AssignedAgent,
            Status = previousTask is null || previousTask.Status == ContentOS.Domain.Enums.TaskStatus.Completed
                ? ContentOS.Domain.Enums.TaskStatus.Ready
                : ContentOS.Domain.Enums.TaskStatus.Pending,
            Kind = TaskKind.Manual,
            ScopeType = scopeType,
            ScopeId = request.ScopeId,
            Instructions = request.Instructions ?? string.Empty,
            InputDataJson = "{}",
            OutputDataJson = "{}",
            InsertedAfterTaskId = insertAfterTask?.Id,
            SupersedesTaskId = request.SupersedesTaskId,
            LastUpdatedUtc = DateTime.UtcNow
        };

        _dbContext.ContentWorkflowTasks.Add(manualTask);

        var oldJobStatus = job.Status;
        if (job.Status == "Completed")
        {
            job.Status = "InProgress";
            job.CompletedUtc = null;
        }

        if (manualTask.Status == ContentOS.Domain.Enums.TaskStatus.Ready)
        {
            job.CurrentStage = manualTask.StageName;
            job.LastUpdatedUtc = DateTime.UtcNow;
        }

        AddMutationLog(
            workflowJobId,
            manualTask.Id,
            WorkflowMutationType.InjectManualTask,
            new
            {
                jobStatus = oldJobStatus,
                insertAfterTaskId = insertAfterTask?.Id,
                insertionOrder
            },
            new
            {
                manualTaskId = manualTask.Id,
                manualTask.DisplayOrder,
                manualTask.Kind,
                manualTask.ScopeType,
                manualTask.ScopeId,
                manualTask.SupersedesTaskId
            },
            request.CreatedBy,
            request.Reason,
            request.Source,
            request.CorrelationId, request.ActorType);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WorkflowTaskMutationResultDto
        {
            WorkflowJobId = workflowJobId,
            TaskId = manualTask.Id,
            DisplayOrder = manualTask.DisplayOrder,
            Status = manualTask.Status.ToString(),
            Kind = manualTask.Kind.ToString(),
            ScopeType = manualTask.ScopeType.ToString(),
            ScopeId = manualTask.ScopeId
        };
    }

    public async Task<WorkflowControlResultDto> ReopenWorkflowAsync(Guid workflowJobId, ReopenWorkflowRequestDto request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");

        if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(job.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(job.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only completed, blocked, or cancelled workflows can be reopened.");
        }

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == workflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        var oldStatus = job.Status;
        var taskToReady = tasks.LastOrDefault(x => x.Status == ContentOS.Domain.Enums.TaskStatus.Rejected)
            ?? tasks.LastOrDefault(x => x.Status == ContentOS.Domain.Enums.TaskStatus.Failed)
            ?? tasks.LastOrDefault();

        if (taskToReady is null)
        {
            throw new InvalidOperationException("Workflow has no tasks to reopen.");
        }

        if (taskToReady.Status is ContentOS.Domain.Enums.TaskStatus.Rejected or ContentOS.Domain.Enums.TaskStatus.Failed or ContentOS.Domain.Enums.TaskStatus.Completed)
        {
            taskToReady.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
            taskToReady.CompletedUtc = null;
            taskToReady.LastUpdatedUtc = DateTime.UtcNow;
        }

        job.Status = "InProgress";
        job.CurrentStage = taskToReady.StageName;
        job.CompletedUtc = null;
        job.ErrorMessage = request.Reason ?? string.Empty;
        job.LastUpdatedUtc = DateTime.UtcNow;

        AddMutationLog(
            workflowJobId,
            taskToReady.Id,
            WorkflowMutationType.ReopenWorkflow,
            new { status = oldStatus },
            new { job.Status, taskId = taskToReady.Id, taskStatus = taskToReady.Status.ToString() },
            request.CreatedBy,
            request.Reason,
            request.Source,
            request.CorrelationId, request.ActorType);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WorkflowControlResultDto
        {
            WorkflowJobId = workflowJobId,
            TaskId = taskToReady.Id,
            JobStatus = job.Status,
            TaskStatus = taskToReady.Status.ToString(),
            Message = $"Workflow reopened at task '{taskToReady.Name}'."
        };
    }

    public async Task<WorkflowTaskMutationResultDto> ReorderTaskAsync(Guid workflowJobId, ReorderWorkflowTaskRequestDto request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow job not found.");

        EnsureMutable(job.Status, allowPaused: true);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == workflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        var task = tasks.FirstOrDefault(x => x.Id == request.TaskId)
            ?? throw new InvalidOperationException("Task not found.");

        if (task.Status is ContentOS.Domain.Enums.TaskStatus.Running or ContentOS.Domain.Enums.TaskStatus.InProgress)
        {
            throw new InvalidOperationException("Running tasks cannot be reordered.");
        }

        if (request.NewDisplayOrder < 1 || request.NewDisplayOrder > tasks.Count)
        {
            throw new InvalidOperationException("New display order is out of range.");
        }

        if (task.ParentTaskId.HasValue)
        {
            var parentTask = tasks.FirstOrDefault(x => x.Id == task.ParentTaskId.Value);
            if (parentTask is not null && request.NewDisplayOrder <= parentTask.DisplayOrder)
            {
                throw new InvalidOperationException("Cannot reorder across dependency boundary.");
            }
        }

        var oldOrder = task.DisplayOrder;
        if (oldOrder == request.NewDisplayOrder)
        {
            return new WorkflowTaskMutationResultDto
            {
                WorkflowJobId = workflowJobId,
                TaskId = task.Id,
                DisplayOrder = task.DisplayOrder,
                Status = task.Status.ToString(),
                Kind = task.Kind.ToString(),
                ScopeType = task.ScopeType.ToString(),
                ScopeId = task.ScopeId
            };
        }

        foreach (var other in tasks)
        {
            if (other.Id == task.Id)
            {
                continue;
            }

            if (oldOrder < request.NewDisplayOrder)
            {
                if (other.DisplayOrder > oldOrder && other.DisplayOrder <= request.NewDisplayOrder)
                {
                    other.DisplayOrder -= 1;
                }
            }
            else
            {
                if (other.DisplayOrder >= request.NewDisplayOrder && other.DisplayOrder < oldOrder)
                {
                    other.DisplayOrder += 1;
                }
            }

            other.LastUpdatedUtc = DateTime.UtcNow;
        }

        task.DisplayOrder = request.NewDisplayOrder;
        task.LastUpdatedUtc = DateTime.UtcNow;

        AddMutationLog(
            workflowJobId,
            task.Id,
            WorkflowMutationType.ReorderTask,
            new { displayOrder = oldOrder },
            new { displayOrder = task.DisplayOrder },
            request.CreatedBy,
            request.Reason,
            request.Source,
            request.CorrelationId, request.ActorType);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WorkflowTaskMutationResultDto
        {
            WorkflowJobId = workflowJobId,
            TaskId = task.Id,
            DisplayOrder = task.DisplayOrder,
            Status = task.Status.ToString(),
            Kind = task.Kind.ToString(),
            ScopeType = task.ScopeType.ToString(),
            ScopeId = task.ScopeId
        };
    }

    private static void EnsureMutable(string status, bool allowCompleted = false, bool allowPaused = false)
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

    private void AddMutationLog(Guid workflowJobId, Guid? taskId, string mutationType, object oldValues, object newValues, string? actor, string? reason, string source, string? correlationId, string actorType = "User")
    {
        var previousStatus = oldValues.GetType().GetProperty("status")?.GetValue(oldValues)?.ToString() ?? string.Empty;
        var newStatus = newValues.GetType().GetProperty("status")?.GetValue(newValues)?.ToString() ?? string.Empty;

        _dbContext.WorkflowMutationLogs.Add(new WorkflowMutationLog
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = workflowJobId,
            ContentWorkflowTaskId = taskId,
            MutationType = mutationType,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            OldValuesJson = JsonSerializer.Serialize(oldValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            ActorIdentity = actor ?? "unknown",
            ActorType = actorType,
            Reason = reason ?? string.Empty,
            Source = source,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow
        });
    }
}
