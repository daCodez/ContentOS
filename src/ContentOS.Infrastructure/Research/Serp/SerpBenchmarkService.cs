using System.Text.RegularExpressions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Serp;

public sealed class SerpBenchmarkService : ISerpBenchmarkService
{
    private readonly IResearchSearchClient _searchClient;

    public SerpBenchmarkService(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public async Task<SerpBenchmarkReport> BuildAsync(ContentIdea idea, CancellationToken cancellationToken = default)
    {
        var primaryKeyword = (idea.PrimaryKeyword ?? string.Empty).Trim();
        var relatedKeywords = ParseJsonArray(idea.SecondaryKeywordsJson)
            .Where(x => !string.Equals(x, primaryKeyword, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();

        if (string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return new SerpBenchmarkReport();
        }

        var results = await _searchClient.SearchAsync(primaryKeyword, searchDepth: "advanced", maxResults: 10, cancellationToken: cancellationToken);
        var snapshots = new List<SerpResultSnapshot>();
        var relatedCounts = relatedKeywords.ToDictionary(x => x, _ => new List<int>(), StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            var content = string.Join("\n", new[] { result.Title, result.Content }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var wordCount = CountWords(content);
            var primaryCount = CountPhraseOccurrences(content, primaryKeyword);
            var headingLikeCount = CountHeadingLikeBlocks(result.Content);

            snapshots.Add(new SerpResultSnapshot
            {
                Title = result.Title,
                Url = result.Url,
                WordCount = wordCount,
                PrimaryKeywordCount = primaryCount,
                HeadingLikeCount = headingLikeCount
            });

            foreach (var keyword in relatedKeywords)
            {
                relatedCounts[keyword].Add(CountPhraseOccurrences(content, keyword));
            }
        }

        var averageWordCount = snapshots.Count == 0 ? 0m : snapshots.Average(x => (decimal)x.WordCount);
        var averagePrimaryKeywordCount = snapshots.Count == 0 ? 0m : snapshots.Average(x => (decimal)x.PrimaryKeywordCount);
        var averageHeadingCount = snapshots.Count == 0 ? 0m : snapshots.Average(x => (decimal)x.HeadingLikeCount);
        var relatedKeywordBenchmarks = relatedCounts
            .Select(pair => new SerpKeywordBenchmark
            {
                Keyword = pair.Key,
                AverageCount = pair.Value.Count == 0 ? 0m : pair.Value.Average(x => (decimal)x),
                RecommendedMin = Math.Max(1, (int)Math.Floor(pair.Value.Count == 0 ? 1m : pair.Value.Average(x => (decimal)x) * 0.8m)),
                RecommendedMax = Math.Max(1, (int)Math.Ceiling(pair.Value.Count == 0 ? 2m : pair.Value.Average(x => (decimal)x) * 1.25m))
            })
            .OrderByDescending(x => x.AverageCount)
            .ToList();

        return new SerpBenchmarkReport
        {
            Query = primaryKeyword,
            ResultCount = snapshots.Count,
            AverageWordCount = Math.Round((decimal)averageWordCount, 1),
            AveragePrimaryKeywordCount = Math.Round((decimal)averagePrimaryKeywordCount, 1),
            AverageHeadingCount = Math.Round((decimal)averageHeadingCount, 1),
            AverageRelatedKeywordHits = relatedKeywordBenchmarks.Count == 0 ? 0 : Math.Round(relatedKeywordBenchmarks.Average(x => x.AverageCount), 1),
            RecommendedWordCountMin = Math.Max(1200, (int)Math.Floor(averageWordCount * 0.9m)),
            RecommendedWordCountMax = Math.Max(1600, (int)Math.Ceiling(averageWordCount * 1.1m)),
            RecommendedPrimaryKeywordMin = Math.Max(1, (int)Math.Floor(averagePrimaryKeywordCount * 0.85m)),
            RecommendedPrimaryKeywordMax = Math.Max(2, (int)Math.Ceiling(averagePrimaryKeywordCount * 1.15m)),
            RelatedKeywords = relatedKeywordBenchmarks,
            Results = snapshots
        };
    }

    private static List<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static int CountWords(string value)
        => string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static int CountPhraseOccurrences(string content, string phrase)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(phrase)) return 0;
        return Regex.Matches(content, $@"(?i)\b{Regex.Escape(phrase)}\b").Count;
    }

    private static int CountHeadingLikeBlocks(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Trim().Length > 20 && line.Trim().Length < 90 && !line.Trim().EndsWith('.'));
    }
}
