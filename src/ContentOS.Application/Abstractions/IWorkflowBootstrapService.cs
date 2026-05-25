namespace ContentOS.Application.Abstractions;

public interface IWorkflowBootstrapService
{
    Task<Guid> ApproveIdeaAndCreateWorkflowAsync(Guid contentIdeaId, string approvedBy, CancellationToken cancellationToken = default);
    Task<Guid> ApproveIdeaRecordAndCreateArticleWorkflowAsync(Guid ideaRecordId, string approvedBy, CancellationToken cancellationToken = default);
}
