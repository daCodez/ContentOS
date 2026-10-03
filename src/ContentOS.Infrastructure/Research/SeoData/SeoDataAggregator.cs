using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SeoData;

public class SeoDataAggregator
{
	private readonly IEnumerable<ISeoDataProvider> _providers;

	private readonly ILogger<SeoDataAggregator> _logger;

	public SeoDataAggregator(IEnumerable<ISeoDataProvider> providers, ILogger<SeoDataAggregator> logger)
	{
		_providers = providers;
		_logger = logger;
	}

	public async Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		foreach (ISeoDataProvider provider in _providers)
		{
			try
			{
				SeoKeywordMetrics result = await provider.GetKeywordMetricsAsync(keyword, ct);
				if ((object)result != null)
				{
					_logger.LogDebug("Keyword metrics for '{Keyword}' provided by {Provider}", keyword, provider.ProviderName);
					return result;
				}
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Provider {Provider} failed for keyword '{Keyword}'", provider.ProviderName, keyword);
			}
		}
		_logger.LogDebug("No keyword metrics available for '{Keyword}' from any provider", keyword);
		return null;
	}

	public async Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default(CancellationToken))
	{
		foreach (ISeoDataProvider provider in _providers)
		{
			try
			{
				SeoTrendData result = await provider.GetTrendDataAsync(keyword, region, timeframe, ct);
				if ((object)result != null)
				{
					_logger.LogDebug("Trend data for '{Keyword}' provided by {Provider}", keyword, provider.ProviderName);
					return result;
				}
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Provider {Provider} failed for trend data '{Keyword}'", provider.ProviderName, keyword);
			}
		}
		return null;
	}

	public async Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default(CancellationToken))
	{
		List<string> allKeywords = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ISeoDataProvider provider in _providers)
		{
			try
			{
				foreach (string kw in await provider.GetRelatedKeywordsAsync(keyword, ct))
				{
					if (seen.Add(kw))
					{
						allKeywords.Add(kw);
					}
				}
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Provider {Provider} failed for related keywords '{Keyword}'", provider.ProviderName, keyword);
			}
		}
		return allKeywords;
	}

	public async Task<Dictionary<string, SeoKeywordMetrics>> GetBulkKeywordMetricsAsync(IEnumerable<string> keywords, CancellationToken ct = default(CancellationToken))
	{
		Dictionary<string, SeoKeywordMetrics> results = new Dictionary<string, SeoKeywordMetrics>(StringComparer.OrdinalIgnoreCase);
		foreach (string keyword in keywords)
		{
			SeoKeywordMetrics metrics = await GetKeywordMetricsAsync(keyword, ct);
			if ((object)metrics != null)
			{
				results[keyword] = metrics;
			}
		}
		return results;
	}
}
