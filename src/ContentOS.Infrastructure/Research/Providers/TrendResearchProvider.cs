using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

public class TrendResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public TrendResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "TrendResearchProvider";

    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();
        var trendSeeds = context.SeedTopics.Take(8).ToList();

        foreach (var seed in trendSeeds)
        {
            foreach (var query in BuildTrendQueries(context, seed))
            {
                var results = await _searchClient.SearchAsync(
                    query: query,
                    searchDepth: "advanced",
                    maxResults: Math.Max(context.MaxFindingsPerProvider, 8),
                    cancellationToken: cancellationToken);

                findings.AddRange(results.Select(result => new ResearchFinding
                {
                    ProviderName = Name,
                    SourceType = "Trend",
                    SourceTitle = result.Title,
                    SourceUrl = result.Url,
                    ObservedPhrase = BuildObservedPhrase(seed, result),
                    PainPoint = BuildPainPoint(seed, result.Content),
                    TopicSuggestion = BuildTopicSuggestion(seed, result),
                    KeywordSuggestion = BuildKeywordSuggestion(seed, result),
                    IntentGuess = InferIntent(seed, result),
                    Notes = BuildTrendNote(result.Content),
                    MonetizationHint = "Trend-backed budgeting topic with timely search demand"
                }));
            }
        }

        return findings;
    }

    private static IEnumerable<string> BuildTrendQueries(ResearchContext context, string seed)
    {
        yield return $"{context.Niche} {seed} trend 2026 rising searches";
        yield return $"{seed} 2026 trend budgeting topic rising interest";
        yield return $"{seed} seasonal budgeting trend current year";
    }

    private static string BuildObservedPhrase(string seed, SearchResult result)
        => !string.IsNullOrWhiteSpace(result.Title) ? result.Title : seed;

    private static string BuildPainPoint(string seed, string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            var sentence = FirstSentence(content);
            if (!string.IsNullOrWhiteSpace(sentence)) return sentence;
        }

        return $"Readers are actively looking for timely help with {seed}.";
    }

    private static string BuildTopicSuggestion(string seed, SearchResult result)
    {
        var trendHint = ExtractTrendHint(result.Title, result.Content);
        var titleSeed = ToTitle(seed);

        if (!string.IsNullOrWhiteSpace(trendHint))
        {
            return $"{titleSeed}: {trendHint}";
        }

        return $"{titleSeed}: What Is Changing Right Now";
    }

    private static string BuildKeywordSuggestion(string seed, SearchResult result)
    {
        var lowerTitle = (result.Title ?? string.Empty).ToLowerInvariant();
        if (lowerTitle.Contains("2026")) return $"{seed} 2026";
        if (lowerTitle.Contains("trend")) return $"{seed} trends";
        if (lowerTitle.Contains("season")) return $"{seed} seasonal";
        return seed;
    }

    private static string InferIntent(string seed, SearchResult result)
    {
        var text = $"{result.Title} {result.Content}".ToLowerInvariant();
        if (text.Contains("best") || text.Contains("top") || text.Contains("tool")) return "Commercial Investigation";
        if (text.Contains("vs") || text.Contains("compare")) return "Comparison";
        return "Informational";
    }

    private static string BuildTrendNote(string content)
    {
        var sentence = FirstSentence(content);
        if (!string.IsNullOrWhiteSpace(sentence))
        {
            return $"Trend signal: {sentence}";
        }

        return "Trend-backed demand detected from current search coverage.";
    }

    private static string ExtractTrendHint(string title, string content)
    {
        var text = $"{title} {content}".ToLowerInvariant();
        if (text.Contains("seasonal")) return "Seasonal Shifts Beginners Should Plan For";
        if (text.Contains("2026")) return "What Beginners Need to Know in 2026";
        if (text.Contains("inflation")) return "How Inflation Is Changing the Math";
        if (text.Contains("grocery")) return "What Rising Grocery Costs Mean for Your Plan";
        if (text.Contains("subscription")) return "How Subscription Creep Is Affecting Monthly Plans";
        if (text.Contains("buy now pay later") || text.Contains("bnpl")) return "How Buy Now Pay Later Habits Break Budgeting";
        return string.Empty;
    }

    private static string FirstSentence(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var split = value.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        return split.FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
}
