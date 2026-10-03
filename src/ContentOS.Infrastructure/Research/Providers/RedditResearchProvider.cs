using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Preserves collected source wording and excerpts; keywords remain unmeasured suggestions.</summary>
public class RedditResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public RedditResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "RedditResearchProvider";

    /// <summary>Collects observed questions and provenance without seed-based article claims.</summary>
    /// <param name="context">Niche and retrieval seeds, not findings.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Collected findings; unavailable searches propagate without fabricated evidence.</returns>
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

            findings.AddRange(results.Where(result => (ResearchEvidenceHandoff.MatchesDomain(result.Url,"reddit.com") || ResearchEvidenceHandoff.MatchesDomain(result.Url,"quora.com")) && !string.IsNullOrWhiteSpace(ResearchEvidenceHandoff.CleanExcerpt(result.Content))).DistinctBy(result=>result.Url).Select(result => new ResearchFinding
            {
                ProviderName = Name,
                SourceType = ResearchEvidenceHandoff.MatchesDomain(result.Url,"reddit.com") ? "Reddit" : "Quora",
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
