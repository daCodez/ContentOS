using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Collects web coverage; this provider does not acquire Google Trends measurements.</summary>
public class TrendResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public TrendResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "TrendResearchProvider";

    /// <summary>Retains source wording while explicitly marking trend demand unknown.</summary>
    /// <param name="context">Retrieval niche and seeds.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Web coverage only, never measured trends or keyword volumes.</returns>
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
                    SourceType = "WebCoverage",
                    SourceTitle = result.Title,
                    SourceUrl = result.Url,
                SourceExcerpt = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                    ObservedPhrase = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                    PainPoint = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                    TopicSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                    KeywordSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                    IntentGuess = "Informational",
                    Notes = "Web coverage only; Google Trends direction, keyword volume and audience demand are unknown.",
                    MonetizationHint = string.Empty
                }));
            }
        }

        return findings.DistinctBy(f => f.SourceUrl).ToList();
    }

    private static IEnumerable<string> BuildTrendQueries(ResearchContext context, string seed)
    {
        yield return $"{context.Niche} {seed} recent questions";
        yield return $"{seed} current practical problems";
        yield return $"{seed} seasonal budgeting trend current year";
    }

}
