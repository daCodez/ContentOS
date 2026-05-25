using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

public class RedditResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public RedditResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "RedditResearchProvider";

    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();

        foreach (var seed in context.SeedTopics.Take(5))
        {
            var results = await _searchClient.SearchAsync(
                query: $"site:reddit.com OR site:quora.com {context.Niche} {seed} beginner problems questions",
                maxResults: context.MaxFindingsPerProvider,
                includeDomains: ["reddit.com", "quora.com"],
                cancellationToken: cancellationToken);

            findings.AddRange(results.Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = "Reddit",
                SourceTitle = result.Title,
                SourceUrl = result.Url,
                ObservedPhrase = ExtractObservedPhrase(result),
                PainPoint = ExtractPainPoint(seed, result),
                TopicSuggestion = SuggestTopic(seed),
                KeywordSuggestion = seed,
                IntentGuess = "Informational",
                Notes = Truncate(result.Content, 220)
            }));
        }

        return findings;
    }

    private static string SuggestTopic(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "How to Budget With Irregular Income Without Falling Behind";
        if (lower.Contains("grocer")) return "Why Your Grocery Budget Keeps Failing and How to Fix It";
        if (lower.Contains("different dates") || lower.Contains("bills hit")) return "How to Budget When Your Bills Hit on Different Dates";
        if (lower.Contains("overwhelmed")) return "How to Start Budgeting When You Feel Overwhelmed";
        return $"{ToTitle(seed)}: What Real Beginners Keep Getting Stuck On";
    }

    private static string ExtractObservedPhrase(SearchResult result)
        => FirstSentence(result.Content);

    private static string ExtractPainPoint(string seed, SearchResult result)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "Income changes from month to month, making it hard to budget confidently.";
        if (lower.Contains("grocer")) return "Grocery spending keeps running past the planned budget.";
        if (lower.Contains("different dates") || lower.Contains("bills hit")) return "Bill timing makes monthly cash flow feel unpredictable and hard to manage.";
        if (lower.Contains("overwhelmed")) return "The topic feels confusing and overwhelming for beginners.";
        return FirstSentence(result.Content);
    }

    private static string FirstSentence(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var split = value.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        return split.FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));

    private static string Truncate(string value, int max)
        => string.IsNullOrWhiteSpace(value) || value.Length <= max ? value : value[..max].TrimEnd() + "...";
}
