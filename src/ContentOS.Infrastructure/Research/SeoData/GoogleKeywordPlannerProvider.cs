using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SeoData;

public class GoogleKeywordPlannerProvider : ISeoDataProvider
{
	private readonly ILogger<GoogleKeywordPlannerProvider> _logger;

	public string ProviderName => "Google Keyword Planner";

	public GoogleKeywordPlannerProvider(ILogger<GoogleKeywordPlannerProvider> logger)
	{
		_logger = logger;
	}

	public Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Google Keyword Planner stub: skipping metrics for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult<SeoKeywordMetrics>(null);
	}

	public Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Google Keyword Planner stub: skipping trend data for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult<SeoTrendData>(null);
	}

	public Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Google Keyword Planner stub: skipping related keywords for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult(new List<string>());
	}
}
