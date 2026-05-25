using ContentOS.Application.Research;

namespace ContentOS.Application.Abstractions;

public interface IResearchAgent
{
    Task<RunResearchResult> RunAsync(Guid siteId, int? requestedIdeaCount = null, CancellationToken cancellationToken = default);
}
