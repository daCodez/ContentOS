using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;

namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Collects competitor snippets and bounded observations; suggested keywords remain unmeasured source wording.</summary>
public class CompetitorResearchProvider : IResearchSourceProvider
{
    private readonly IResearchSearchClient _searchClient;

    public CompetitorResearchProvider(IResearchSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public string Name => "CompetitorResearchProvider";

    /// <summary>Collects existing search results, retaining cleaned snippets and their original URLs.</summary>
    /// <param name="context">Approved seed topics and retrieval bounds.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Findings with unverified excerpt-level observations and explicit completeness limits.</returns>
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
                SourceExcerpt = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                ObservedPhrase = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                PainPoint = ResearchEvidenceHandoff.CleanExcerpt(result.Content),
                TopicSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                KeywordSuggestion = ResearchEvidenceHandoff.ObservedQuestion(result.Title, ResearchEvidenceHandoff.CleanExcerpt(result.Content)),
                IntentGuess = "Informational",
                Notes = BuildGapNote(result.Content)
            }));
        }

        return findings.DistinctBy(f=>f.SourceUrl).ToList();
    }

    /// <summary>Describes only what is observable in retrieved text; absence is a research question, not a proven competitor gap.</summary>
    /// <param name="content">Untrusted retrieved search content.</param>
    /// <returns>Bounded observations and explicit limits without asserting source quality or inventing a gap.</returns>
    private static string BuildGapNote(string content)
    {
        var excerpt = ResearchEvidenceHandoff.CleanExcerpt(content);
        if (string.IsNullOrWhiteSpace(excerpt))
        {
            return "No usable retrieved excerpt. Competitor coverage and content gaps are unknown; obtain evidence before making claims.";
        }
        var hasNumbers = excerpt.Any(char.IsDigit);
        var hasSteps = System.Text.RegularExpressions.Regex.IsMatch(excerpt, @"\bstep\s*\d|\bfirst\b|\bnext\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return $"Retrieved excerpt contains {(hasNumbers ? "numeric text" : "no numeric text")} and {(hasSteps ? "step markers" : "no step markers")}. "
            + "These are text observations, not verified examples or instructions. Any missing element is only an excerpt-level research question; full-page gaps are unverified.";
    }

}
