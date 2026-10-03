using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Retains commercial-source wording and excerpts without manufacturing seed-based article topics.</summary>
public class MonetizationResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public MonetizationResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "MonetizationResearchProvider";

    /// <summary>Collects tool-related search snippets with explicit inference limits.</summary>
    /// <param name="context">Retrieval seeds and bounds.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Unverified commercial-source observations with original provenance.</returns>
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
                SourceExcerpt = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                ObservedPhrase = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                PainPoint = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                TopicSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                KeywordSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                IntentGuess = "Commercial Investigation",
                MonetizationHint = "Apps, templates, printables, or affiliate tools",
                Notes = "Commercial intent and tool fit are inferred from the retrieval query, not verified audience demand. Keyword metrics are unknown."
            }));
        }

        return findings.DistinctBy(f=>f.SourceUrl).ToList();
    }

}
