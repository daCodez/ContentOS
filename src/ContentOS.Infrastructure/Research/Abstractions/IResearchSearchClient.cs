namespace ContentOS.Infrastructure.Research.Abstractions;

public interface IResearchSearchClient
{
    Task<IReadOnlyCollection<SearchResult>> SearchAsync(
        string query,
        string searchDepth = "basic",
        int maxResults = 5,
        IReadOnlyCollection<string>? includeDomains = null,
        IReadOnlyCollection<string>? excludeDomains = null,
        CancellationToken cancellationToken = default);
}
