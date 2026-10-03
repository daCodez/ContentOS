using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Preserves collected source wording and excerpts; keywords remain unmeasured suggestions.</summary>
public class SearchIntentResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public SearchIntentResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "SearchIntentResearchProvider";

    /// <summary>Collects observed questions and provenance without seed-based article claims.</summary>
    /// <param name="context">Niche and retrieval seeds, not findings.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Collected findings; unavailable searches propagate without fabricated evidence.</returns>
    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context, CancellationToken cancellationToken)
    {
        var findings = new List<ResearchFinding>();

        foreach (var seed in context.SeedTopics.Take(3))
        {
            var results = await _searchClient.SearchAsync(
                query: $"{context.Niche} {seed} questions",
                maxResults: context.MaxFindingsPerProvider,
                cancellationToken: cancellationToken);

            findings.AddRange(results.Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = "SearchIntent",
                SourceTitle = result.Title,
                SourceUrl = result.Url,
                SourceExcerpt = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                ObservedPhrase = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                PainPoint = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                TopicSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                KeywordSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                IntentGuess = "Informational",
                Notes = "Collected search wording, not measured keyword demand. Audience demand, volume and difficulty are unknown; snippets may omit discussion context."
            }));
        }

        return findings.DistinctBy(f => f.SourceUrl).ToList();
    }

}
