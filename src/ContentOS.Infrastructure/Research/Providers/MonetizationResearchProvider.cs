using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

public class MonetizationResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public MonetizationResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "MonetizationResearchProvider";

    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();

        foreach (var seed in context.SeedTopics.Take(6))
        {
            var results = await _searchClient.SearchAsync(
                query: $"best app tool template for {seed}",
                maxResults: context.MaxFindingsPerProvider,
                cancellationToken: cancellationToken);

            findings.AddRange(results.Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = "Monetization",
                SourceTitle = result.Title,
                SourceUrl = result.Url,
                ObservedPhrase = result.Title,
                PainPoint = "Readers want a practical tool or system they can actually use.",
                TopicSuggestion = SuggestCommercialTopic(seed),
                KeywordSuggestion = SuggestCommercialKeyword(seed),
                IntentGuess = InferIntent(seed),
                MonetizationHint = "Apps, templates, printables, or affiliate tools",
                Notes = Truncate(result.Content, 220)
            }));
        }

        return findings;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrWhiteSpace(value) || value.Length <= max ? value : value[..max].TrimEnd() + "...";

    private static string SuggestCommercialTopic(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "Best Budget Apps for Irregular Income";
        if (lower.Contains("grocer")) return "Best Grocery Budget Apps and Trackers for Beginners";
        if (lower.Contains("different dates") || lower.Contains("variable bills")) return "Best Bill Tracker Apps for Uneven Due Dates";
        if (lower.Contains("printable")) return "Best Budget Planner Printables for Beginners";
        if (lower.Contains("cash stuffing")) return "Cash Stuffing Alternatives That Are Easier to Maintain";
        if (lower.Contains("weekly vs monthly")) return "Weekly vs Monthly Budgeting Apps: Which Is Easier to Stick To?";
        return $"Best {ToTitle(seed)} Tools for Beginners";
    }

    private static string SuggestCommercialKeyword(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("irregular income")) return "best budget app for irregular income";
        if (lower.Contains("grocer")) return "best grocery budget app";
        if (lower.Contains("different dates") || lower.Contains("variable bills")) return "best bill tracker app for budgeting";
        if (lower.Contains("printable")) return "best budget planner printable";
        if (lower.Contains("cash stuffing")) return "cash stuffing alternatives";
        if (lower.Contains("weekly vs monthly")) return "weekly vs monthly budgeting app";
        return $"best {seed}";
    }

    private static string InferIntent(string seed)
    {
        var lower = seed.ToLowerInvariant();
        if (lower.Contains("alternatives") || lower.Contains("vs ") || lower.Contains("weekly vs monthly")) return "Comparison";
        return "Commercial Investigation";
    }

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
}
