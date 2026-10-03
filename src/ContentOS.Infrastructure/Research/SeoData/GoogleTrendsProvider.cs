using ContentOS.Infrastructure.Research.Abstractions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SeoData;

/// <summary>Unconnected Google Trends compatibility provider; returns unknowns instead of manufacturing measurements from search snippets.</summary>
/// <remarks>No live Trends integration exists. Source searches, scores and seeded randomness cannot measure volume, difficulty or trends.</remarks>
public class GoogleTrendsProvider : ISeoDataProvider
{
    /// <summary>Identifies this unconnected compatibility registration.</summary>
    public string ProviderName => "Google Trends";

    /// <summary>Preserves the existing registration contract without using search results as measurements.</summary>
    /// <param name="searchClient">Existing search dependency; not invoked to manufacture metrics.</param>
    /// <param name="logger">Existing diagnostic dependency.</param>
    public GoogleTrendsProvider(IResearchSearchClient searchClient, ILogger<GoogleTrendsProvider> logger) { }

    /// <summary>Returns unknown keyword metrics until an actual measurement provider is connected.</summary>
    /// <param name="keyword">Requested natural phrase.</param>
    /// <param name="ct">Cancellation for the request.</param>
    /// <returns>No measured volume, difficulty, CPC or trend score.</returns>
    public Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<SeoKeywordMetrics?>(null);
    }

    /// <summary>Returns unknown trend history; never creates random data points.</summary>
    /// <param name="keyword">Requested phrase.</param>
    /// <param name="region">Requested region.</param>
    /// <param name="timeframe">Requested interval.</param>
    /// <param name="ct">Cancellation for the request.</param>
    /// <returns>No observed trend series.</returns>
    public Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<SeoTrendData?>(null);
    }

    /// <summary>Returns no measured related queries; search-result headlines are not keyword observations.</summary>
    /// <param name="keyword">Requested phrase.</param>
    /// <param name="ct">Cancellation for the request.</param>
    /// <returns>An empty set until actual related-query evidence is available.</returns>
    public Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new List<string>());
    }
}
