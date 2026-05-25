using ContentOS.Application.Abstractions;
using ContentOS.Application.Commands;
using ContentOS.Application.Research;
using MediatR;

namespace ContentOS.Application.Handlers;

public class RunResearchCommandHandler : IRequestHandler<RunResearchCommand, RunResearchResult>
{
    private readonly IResearchAgent _researchAgent;

    public RunResearchCommandHandler(IResearchAgent researchAgent)
    {
        _researchAgent = researchAgent;
    }

    public Task<RunResearchResult> Handle(RunResearchCommand request, CancellationToken cancellationToken)
    {
        return _researchAgent.RunAsync(request.SiteId, request.RequestedIdeaCount, cancellationToken);
    }
}
