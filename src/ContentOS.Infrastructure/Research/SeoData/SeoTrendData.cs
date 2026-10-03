using System.Collections.Generic;

namespace ContentOS.Infrastructure.Research.SeoData;

public record SeoTrendData(string Keyword, List<TrendPoint> Points, string Region = "US", string Timeframe = "last-12-months");
