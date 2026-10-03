using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SeoData;

public class SearchConsoleProvider : ISeoDataProvider
{
	private readonly ILogger<SearchConsoleProvider> _logger;

	public string ProviderName => "Search Console";

	public SearchConsoleProvider(ILogger<SearchConsoleProvider> logger)
	{
		_logger = logger;
	}

	public Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Search Console stub: skipping metrics for '{Keyword}' (no service account configured)", keyword);
		return Task.FromResult<SeoKeywordMetrics>(null);
	}

	public Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Search Console stub: skipping trend data for '{Keyword}' (no service account configured)", keyword);
		return Task.FromResult<SeoTrendData>(null);
	}

	public Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Search Console stub: skipping related keywords for '{Keyword}' (no service account configured)", keyword);
		return Task.FromResult(new List<string>());
	}
}
