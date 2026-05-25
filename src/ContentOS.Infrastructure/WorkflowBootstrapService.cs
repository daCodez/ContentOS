using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public class WorkflowBootstrapService : IWorkflowBootstrapService
{
    private readonly ContentOsDbContext _dbContext;
    private readonly WorkflowPipelineOrchestrator _workflowPipelineOrchestrator;

    public WorkflowBootstrapService(ContentOsDbContext dbContext, WorkflowPipelineOrchestrator workflowPipelineOrchestrator)
    {
        _dbContext = dbContext;
        _workflowPipelineOrchestrator = workflowPipelineOrchestrator;
    }

    public async Task<Guid> ApproveIdeaAndCreateWorkflowAsync(Guid contentIdeaId, string approvedBy, CancellationToken cancellationToken = default)
    {
        var idea = await _dbContext.ContentIdeas.FirstOrDefaultAsync(x => x.Id == contentIdeaId, cancellationToken)
            ?? throw new InvalidOperationException("Content idea not found.");

        var ideaRecord = await _dbContext.IdeaRecords
            .Where(x => x.SiteId == idea.SiteId && x.IdeaTitle == idea.Title)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (ideaRecord is null)
        {
            throw new InvalidOperationException("No IdeaRecord exists for this content idea yet. Run idea generation again before approval.");
        }

        return await ApproveIdeaRecordAndCreateArticleWorkflowAsync(ideaRecord.Id, approvedBy, cancellationToken);
    }

    public async Task<Guid> ApproveIdeaRecordAndCreateArticleWorkflowAsync(Guid ideaRecordId, string approvedBy, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var idea = await _dbContext.IdeaRecords.FirstOrDefaultAsync(x => x.Id == ideaRecordId, cancellationToken)
            ?? throw new InvalidOperationException("Idea record not found.");

        if (idea.Status != IdeaRecordStatus.Pending)
        {
            throw new InvalidOperationException("Only pending ideas can be approved.");
        }

        // Set status in-memory before passing to orchestrator
        idea.Status = IdeaRecordStatus.Approved;
        idea.ApprovedUtc = DateTime.UtcNow;
        idea.ApprovedBy = approvedBy;
        idea.UpdatedUtc = DateTime.UtcNow;

        var runId = await _workflowPipelineOrchestrator.StartArticleWorkflowAsync(idea, approvedBy, cancellationToken);

        // Persist via ExecuteUpdateAsync to avoid EF Core concurrency exceptions
        await _dbContext.IdeaRecords
            .Where(x => x.Id == ideaRecordId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, IdeaRecordStatus.Approved)
                .SetProperty(x => x.ApprovedUtc, DateTime.UtcNow)
                .SetProperty(x => x.ApprovedBy, approvedBy)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return runId;
    }

}
