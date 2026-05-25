using ContentOS.Application.Abstractions;
using ContentOS.Application.Commands;
using MediatR;

namespace ContentOS.Application.Handlers;

public class ApproveContentIdeaCommandHandler : IRequestHandler<ApproveContentIdeaCommand, Guid>
{
    private readonly IWorkflowBootstrapService _workflowBootstrapService;

    public ApproveContentIdeaCommandHandler(IWorkflowBootstrapService workflowBootstrapService)
    {
        _workflowBootstrapService = workflowBootstrapService;
    }

    public Task<Guid> Handle(ApproveContentIdeaCommand request, CancellationToken cancellationToken)
    {
        return _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(request.ContentIdeaId, request.ApprovedBy, cancellationToken);
    }
}
