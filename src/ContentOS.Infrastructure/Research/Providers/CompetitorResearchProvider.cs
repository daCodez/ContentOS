using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

public class CompetitorResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public CompetitorResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "CompetitorResearchProvider";

    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();

        foreach (var seed in context.SeedTopics.Take(2))
        {
            var results = await _searchClient.SearchAsync(
                query: $"best blog posts about {seed} for beginners",
                maxResults: context.MaxFindingsPerProvider,
                excludeDomains: ["reddit.com", "quora.com"],
                cancellationToken: cancellationToken);

            findings.AddRange(results.Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = "Competitor",
                SourceTitle = result.Title,
                SourceUrl = result.Url,
                ObservedPhrase = result.Title,
                PainPoint = InferPainPoint(seed),
                TopicSuggestion = SuggestTopic(seed),
                KeywordSuggestion = seed,
                IntentGuess = "Informational",
                Notes = BuildGapNote(result.Content)
            }));
        }

        return findings;
    }

    private static string SuggestTopic(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "How to Budget With Irregular Income Without Falling Behind";
        if (lower.Contains("overspending groceries") || lower.Contains("grocer")) return "Why Your Grocery Budget Keeps Failing and How to Fix It";
        if (lower.Contains("different dates") || lower.Contains("bills hit")) return "How to Budget When Your Bills Hit on Different Dates";
        return $"{ToTitle(seed)}: A Better Beginner-Friendly Version";
    }

    private static string InferPainPoint(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "Income changes from month to month, making it hard to budget confidently.";
        if (lower.Contains("overspending groceries") || lower.Contains("grocer")) return "Grocery spending keeps running past the planned budget.";
        if (lower.Contains("different dates") || lower.Contains("bills hit")) return "Bill timing makes monthly cash flow feel unpredictable and hard to manage.";
        return "Competitor content is not making the topic easy enough for beginners.";
    }

    private static string BuildGapNote(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Competitor coverage exists, but the explanation likely needs to be clearer and more practical for beginners.";
        }

        return "Competitor coverage exists, but there is room for a clearer, more practical beginner explanation.";
    }

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
}
