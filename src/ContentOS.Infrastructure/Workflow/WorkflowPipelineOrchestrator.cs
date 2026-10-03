using System.Text.Json;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Workflow;

public class WorkflowPipelineOrchestrator
{
    private readonly ContentOsDbContext _dbContext;

    public WorkflowPipelineOrchestrator(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowDefinitionDto> PublishAsync(WorkflowDefinitionDto draft, string publishedBy, CancellationToken cancellationToken)
    {
        WorkflowDefinitionValidationService.NormalizeAndValidate(draft);

        var family = await _dbContext.WorkflowDefinitionFamilies
            .FirstOrDefaultAsync(x => x.Id == draft.WorkflowDefinitionFamilyId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow family not found.");

        var previousDefinition = await _dbContext.WorkflowDefinitions
            .Where(x => x.WorkflowDefinitionFamilyId == family.Id && x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var newDefinition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionFamilyId = family.Id,
              Version = (await _dbContext.WorkflowDefinitions.Where(x => x.WorkflowDefinitionFamilyId == family.Id)
                  .Select(x => (int?)x.Version).MaxAsync(cancellationToken) ?? 0) + 1,
            Name = draft.Name,
            Description = draft.Description,
            WorkflowType = family.WorkflowType,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            PublishedUtc = DateTime.UtcNow,
            PublishedBy = publishedBy
        };

        _dbContext.WorkflowDefinitions.Add(newDefinition);

        foreach (var actionDto in draft.Actions.OrderBy(x => x.Order))
        {
            var action = new WorkflowActionDefinition
            {
                  Id = Guid.NewGuid(), // Immutable version clones use new physical IDs; source IDs remain in mutation JSON.
                WorkflowDefinitionId = newDefinition.Id,
                CapabilityKey = actionDto.CapabilityKey,
                Name = actionDto.Name,
                Description = actionDto.Description,
                AssignedAgent = actionDto.AssignedAgent,
                Order = actionDto.Order,
                Instructions = actionDto.Instructions,
                IsEnabled = actionDto.IsEnabled,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _dbContext.WorkflowActionDefinitions.Add(action);

            foreach (var stepDto in actionDto.Steps.OrderBy(x => x.Order))
            {
                _dbContext.WorkflowStepDefinitions.Add(new WorkflowStepDefinition
                {
                      Id = Guid.NewGuid(),
                    WorkflowActionDefinitionId = action.Id,
                    CapabilityKey = stepDto.CapabilityKey,
                    Name = stepDto.Name,
                    Purpose = stepDto.Purpose,
                    Order = stepDto.Order,
                    Instructions = stepDto.Instructions,
                    ExpectedOutput = stepDto.ExpectedOutput,
                    IsEnabled = stepDto.IsEnabled,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
        }

        if (previousDefinition is not null)
        {
            previousDefinition.IsActive = false;
        }

        family.ActiveWorkflowDefinitionId = newDefinition.Id;
        family.UpdatedUtc = DateTime.UtcNow;

        _dbContext.WorkflowDefinitionMutations.Add(new WorkflowDefinitionMutation
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = newDefinition.Id,
            MutationType = "PublishVersion",
            Summary = $"Published workflow version {newDefinition.Version} for {newDefinition.Name}",
            OldValueJson = previousDefinition is null ? "{}" : JsonSerializer.Serialize(new { previousDefinition.Id, previousDefinition.Version }),
            NewValueJson = JsonSerializer.Serialize(draft),
            CreatedBy = publishedBy,
            CreatedUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        draft.Id = newDefinition.Id;
        draft.Version = newDefinition.Version;
        draft.IsActive = true;
        draft.LastUpdatedUtc = newDefinition.PublishedUtc;
        draft.WorkflowType = family.WorkflowType.ToString();
        return draft;
    }

    public async Task<Guid> StartArticleWorkflowAsync(IdeaRecord idea, string requestedBy, CancellationToken cancellationToken)
    {
        if (idea.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Article workflow requires approved ideaId.");
        }

        if (idea.Status != IdeaRecordStatus.Approved)
        {
            throw new InvalidOperationException("Idea record is not approved.");
        }

        if (string.IsNullOrWhiteSpace(idea.IdeaTitle))
        {
            throw new InvalidOperationException("Article workflow requires ideaTitle.");
        }

        if (string.IsNullOrWhiteSpace(idea.ReaderProblem))
        {
            throw new InvalidOperationException("Article workflow requires readerProblem.");
        }

        var definition = await GetActiveDefinitionAsync(WorkflowDefinitionType.Article, cancellationToken);
        var run = new WorkflowDefinitionRun
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionFamilyId = definition.WorkflowDefinitionFamilyId,
            WorkflowDefinitionId = definition.Id,
            WorkflowType = definition.WorkflowType,
            Version = definition.Version,
            Status = WorkflowDefinitionRunStatus.Ready,
            TriggeredBy = requestedBy,
            IdeaRecordId = idea.Id,
            InputSnapshotJson = JsonSerializer.Serialize(new
            {
                ideaId = idea.Id,
                ideaTitle = idea.IdeaTitle,
                readerProblem = idea.ReaderProblem,
                audienceType = idea.AudienceType,
                searchIntent = idea.SearchIntent,
                angle = idea.UniquenessAngle
            })
        };

        _dbContext.WorkflowDefinitionRuns.Add(run);
        await MaterializeActionRunsAsync(definition.Id, run.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task<bool> RetryActionAsync(Guid workflowRunId, Guid actionRunId, CancellationToken cancellationToken)
    {
        var workflowRun = await _dbContext.WorkflowDefinitionRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, cancellationToken);
        var actionRun = await _dbContext.WorkflowActionRuns.FirstOrDefaultAsync(x => x.Id == actionRunId && x.WorkflowDefinitionRunId == workflowRunId, cancellationToken);
        if (workflowRun is null || actionRun is null)
        {
            return false;
        }

        if (actionRun.RetryCount >= actionRun.MaxRetry)
        {
            actionRun.Status = WorkflowDefinitionRunStatus.Failed;
            workflowRun.Status = WorkflowDefinitionRunStatus.Failed;
            workflowRun.ErrorMessage = actionRun.ErrorMessage ?? "Retry limit exhausted.";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        actionRun.RetryCount += 1;
        actionRun.Status = WorkflowDefinitionRunStatus.Ready;
        actionRun.LastFailureUtc = DateTime.UtcNow;
        workflowRun.Status = WorkflowDefinitionRunStatus.InProgress;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IdeaRecord?> GetApprovedIdeaAsync(Guid ideaRecordId, CancellationToken cancellationToken)
    {
        return await _dbContext.IdeaRecords.FirstOrDefaultAsync(x => x.Id == ideaRecordId, cancellationToken);
    }

    private async Task<WorkflowDefinition> GetActiveDefinitionAsync(WorkflowDefinitionType type, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.WorkflowType == type && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"No active {type} workflow definition found.");
    }

    private async Task MaterializeActionRunsAsync(Guid workflowDefinitionId, Guid workflowRunId, CancellationToken cancellationToken)
    {
        var actions = await _dbContext.WorkflowActionDefinitions
            .Where(x => x.WorkflowDefinitionId == workflowDefinitionId && x.IsEnabled)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        foreach (var action in actions)
        {
            var actionRun = new WorkflowActionRun
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionRunId = workflowRunId,
                WorkflowActionDefinitionId = action.Id,
                Name = action.Name,
                Order = action.Order,
                Status = action.Order == 1 ? WorkflowDefinitionRunStatus.Ready : WorkflowDefinitionRunStatus.Pending,
                MaxRetry = 3
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
                    Status = action.Order == 1 && step.Order == 1 ? WorkflowDefinitionRunStatus.Ready : WorkflowDefinitionRunStatus.Pending
                });
            }
        }
    }
}
