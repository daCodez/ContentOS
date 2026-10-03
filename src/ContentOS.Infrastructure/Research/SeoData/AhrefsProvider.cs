using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SeoData;

public class AhrefsProvider : ISeoDataProvider
{
	private readonly ILogger<AhrefsProvider> _logger;

	public string ProviderName => "Ahrefs";

	public AhrefsProvider(ILogger<AhrefsProvider> logger)
	{
		_logger = logger;
	}

	public Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Ahrefs provider stub: skipping metrics for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult<SeoKeywordMetrics>(null);
	}

	public Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Ahrefs provider stub: skipping trend data for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult<SeoTrendData>(null);
	}

	public Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		_logger.LogDebug("Ahrefs provider stub: skipping related keywords for '{Keyword}' (no API key configured)", keyword);
		return Task.FromResult(new List<string>());
	}
}
