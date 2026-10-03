namespace ContentOS.Infrastructure.Research.SeoData;

public record SeoKeywordMetrics(string Keyword, int? MonthlyVolume, int? Difficulty, decimal? Cpc, decimal? TrendScore, string? SourceProvider);
