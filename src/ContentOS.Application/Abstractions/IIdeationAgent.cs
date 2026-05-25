using ContentOS.Application.Research;

namespace ContentOS.Application.Abstractions;

public interface IIdeationAgent
{
    Task<IList<CandidateContentIdea>> GenerateIdeasAsync(
        ResearchContext context,
        IList<ResearchFinding> findings,
        CancellationToken cancellationToken = default);
}
