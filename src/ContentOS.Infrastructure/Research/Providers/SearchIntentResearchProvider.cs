using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

public class SearchIntentResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public SearchIntentResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "SearchIntentResearchProvider";

    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();

        foreach (var seed in context.SeedTopics.Take(3))
        {
            var results = await _searchClient.SearchAsync(
                query: $"{seed} people also ask related searches autocomplete beginner",
                maxResults: context.MaxFindingsPerProvider,
                cancellationToken: cancellationToken);

            findings.AddRange(results.Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = "SearchIntent",
                SourceTitle = result.Title,
                SourceUrl = result.Url,
                ObservedPhrase = result.Title,
                PainPoint = ExtractPainPoint(seed, result.Content),
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

        if (lower.Contains("irregular income"))
        {
            return "How to Budget With Irregular Income Without Falling Behind";
        }

        if (lower.Contains("overspending groceries") || lower.Contains("grocer"))
        {
            return "Why Your Grocery Budget Keeps Failing and How to Fix It";
        }

        if (lower.Contains("different dates") || lower.Contains("bills hit"))
        {
            return "How to Budget When Your Bills Hit on Different Dates";
        }

        if (lower.Contains("overwhelmed"))
        {
            return "How to Start Budgeting When You Feel Overwhelmed";
        }

        return $"{ToTitle(seed)} for Beginners";
    }

    private static string ExtractPainPoint(string seed, string value)
    {
        var lowerSeed = seed.ToLowerInvariant();
        if (lowerSeed.Contains("irregular income")) return "Income changes from month to month, making it hard to budget confidently.";
        if (lowerSeed.Contains("overspending groceries") || lowerSeed.Contains("grocer")) return "Grocery spending keeps running past the planned budget.";
        if (lowerSeed.Contains("different dates") || lowerSeed.Contains("bills hit")) return "Bill timing makes monthly cash flow feel unpredictable and hard to manage.";
        if (lowerSeed.Contains("overwhelmed")) return "The topic feels confusing and overwhelming for beginners.";

        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lower = value.ToLowerInvariant();
        if (lower.Contains("overwhelm") || lower.Contains("confus")) return "The topic feels confusing and overwhelming for beginners.";
        if (lower.Contains("beginner")) return "Beginners need a simpler explanation and starting point.";
        return FirstSentence(value);
    }

    private static string FirstSentence(string value)
    {
        var split = value.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        return split.FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrWhiteSpace(value) || value.Length <= max ? value : value[..max].TrimEnd() + "...";

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
}
