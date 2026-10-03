using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContentOS.Infrastructure.Research.SeoData;

public interface ISeoDataProvider
{
	string ProviderName { get; }

	Task<SeoKeywordMetrics?> GetKeywordMetricsAsync(string keyword, CancellationToken ct = default(CancellationToken));

	Task<SeoTrendData?> GetTrendDataAsync(string keyword, string region = "US", string timeframe = "last-12-months", CancellationToken ct = default(CancellationToken));

	Task<List<string>> GetRelatedKeywordsAsync(string keyword, CancellationToken ct = default(CancellationToken));
}
