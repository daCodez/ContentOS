using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public sealed class RerunWorkflowFromTaskCommandHandler : IRequestHandler<RerunWorkflowFromTaskCommand, bool>
{
    private readonly ContentOsDbContext _dbContext;

    public RerunWorkflowFromTaskCommandHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(RerunWorkflowFromTaskCommand request, CancellationToken cancellationToken)
    {
        var job = await _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == request.WorkflowJobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == request.WorkflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (tasks.Count == 0)
        {
            return false;
        }

        var startingTask = tasks.FirstOrDefault(x => x.DisplayOrder == request.StartingDisplayOrder);
        if (startingTask is null)
        {
            return false;
        }

        var taskIdsToReset = tasks
            .Where(x => x.DisplayOrder >= request.StartingDisplayOrder)
            .Select(x => x.Id)
            .ToHashSet();

        var artifactsToDelete = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == request.WorkflowJobId &&
                        ((x.ContentWorkflowTaskId.HasValue && taskIdsToReset.Contains(x.ContentWorkflowTaskId.Value)) ||
                         x.ArtifactType == "FinalArticlePreview"))
            .ToListAsync(cancellationToken);

        if (artifactsToDelete.Count > 0)
        {
            _dbContext.ContentArtifacts.RemoveRange(artifactsToDelete);
        }

        foreach (var task in tasks)
        {
            if (task.DisplayOrder < request.StartingDisplayOrder)
            {
                continue;
            }

            task.Status = task.DisplayOrder == request.StartingDisplayOrder
                ? ContentOS.Domain.Enums.TaskStatus.Ready
                : ContentOS.Domain.Enums.TaskStatus.Pending;
            task.StartedUtc = null;
            task.CompletedUtc = null;
            task.ErrorMessage = string.Empty;
            task.OutputDataJson = "{}";
            task.InputDataJson = task.DisplayOrder == request.StartingDisplayOrder
                ? System.Text.Json.JsonSerializer.Serialize(new
                {
                    rerunRequestedBy = request.RequestedBy,
                    rerunRequestedUtc = DateTime.UtcNow
                })
                : "{}";
            task.LastUpdatedUtc = DateTime.UtcNow;
        }

        job.Status = "InProgress";
        job.CurrentStage = startingTask.StageName;
        job.LastUpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
