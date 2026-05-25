using ContentOS.Application.Research;

namespace ContentOS.Application.Abstractions;

public interface IResearchSourceProvider
{
    string Name { get; }

    Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken);
}
