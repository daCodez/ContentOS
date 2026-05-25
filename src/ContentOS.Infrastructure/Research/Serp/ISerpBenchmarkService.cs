using ContentOS.Domain.Entities;

namespace ContentOS.Infrastructure.Research.Serp;

public interface ISerpBenchmarkService
{
    Task<SerpBenchmarkReport> BuildAsync(ContentIdea idea, CancellationToken cancellationToken = default);
}

public sealed class SerpBenchmarkReport
{
    public string Query { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public decimal AverageWordCount { get; set; }
    public decimal AveragePrimaryKeywordCount { get; set; }
    public decimal AverageHeadingCount { get; set; }
    public decimal AverageRelatedKeywordHits { get; set; }
    public int RecommendedWordCountMin { get; set; }
    public int RecommendedWordCountMax { get; set; }
    public int RecommendedPrimaryKeywordMin { get; set; }
    public int RecommendedPrimaryKeywordMax { get; set; }
    public List<SerpKeywordBenchmark> RelatedKeywords { get; set; } = [];
    public List<SerpResultSnapshot> Results { get; set; } = [];
}

public sealed class SerpKeywordBenchmark
{
    public string Keyword { get; set; } = string.Empty;
    public decimal AverageCount { get; set; }
    public int RecommendedMin { get; set; }
    public int RecommendedMax { get; set; }
}

public sealed class SerpResultSnapshot
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int PrimaryKeywordCount { get; set; }
    public int HeadingLikeCount { get; set; }
}
