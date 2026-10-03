using ContentOS.Infrastructure.Workflow;
using System.Collections.Concurrent;
using System.Text.Json;
using ContentOS.Infrastructure.Research.Abstractions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SearXng;

public class SearXngResearchClient : IResearchSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearXngResearchClient> _logger;
    private readonly SearXngContentExtractor _extractor;
    private readonly ConcurrentDictionary<string, DateTime> _recentExtractions = new();

    public SearXngResearchClient(HttpClient httpClient, ILogger<SearXngResearchClient> logger, SearXngContentExtractor extractor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _extractor = extractor;
    }

    /// <summary>Searches indexed sources with domain constraints; failures remain distinct from valid empty results.</summary>
    /// <param name="query">Retrieval query, never logged.</param><param name="searchDepth">Extraction budget mode.</param>
    /// <param name="maxResults">Maximum results.</param><param name="includeDomains">Allowed domain boundaries.</param>
    /// <param name="excludeDomains">Excluded domain boundaries.</param><param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Collected results, possibly empty on a valid response.</returns>
    /// <exception cref="HttpRequestException">Provider rejected, failed, or returned an invalid response; competition is unknown.</exception>
    public async Task<IReadOnlyCollection<SearchResult>> SearchAsync(
        string query,
        string searchDepth = "basic",
        int maxResults = 5,
        IReadOnlyCollection<string>? includeDomains = null,
        IReadOnlyCollection<string>? excludeDomains = null,
        CancellationToken cancellationToken = default)
    {
        var searxngUrl = Environment.GetEnvironmentVariable("SEARXNG_BASE_URL")
            ?? "http://127.0.0.1:8888";

        var pageno = searchDepth == "advanced" ? 1 : 1;
        var url = $"{searxngUrl}/search?q={Uri.EscapeDataString(query)}&format=json&pageno={pageno}";

        // Keep the operator's configured engine set. Domain queries/filters express forum intent
        // without replacing a working configured engine with unavailable or CAPTCHA-blocked ones.

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Research search was rejected (HTTP {StatusCode}). Check the local search provider before retrying; no results were accepted. Outcome={Outcome}", (int)response.StatusCode, "http-failure");
                throw new HttpRequestException("Research search unavailable: provider rejected the request.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
            {
                throw new HttpRequestException("Research search unavailable: invalid provider response.");
            }

            // Phase 1: Collect and deduplicate raw results
            var rawResults = CollectResults(resultsElement, includeDomains, excludeDomains);

            // Phase 2: Score and rank with freshness + domain weighting + answer relevance
            var scored = rawResults
                .Select(r => ScoreResult(r, query))
                .OrderByDescending(r => r.Score)
                .ToList();

            // Phase 3: Content extraction for top results (async, limited concurrency)
            var topToExtract = scored.Take(searchDepth == "advanced" ? maxResults : Math.Min(maxResults, 5)).ToList();
            await EnrichWithExtractedContent(topToExtract, cancellationToken);

            // Phase 4: Final cleanup and trim
            var finalResults = topToExtract
                .Select(CleanSnippet)
                .Take(maxResults)
                .Select(r => new SearchResult
                {
                    Title = r.Title,
                    Url = r.Url,
                    Content = r.Snippet,
                    Score = r.Score
                })
                .ToList();

            return finalResults;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            WorkflowDiagnostics.LogFailure(_logger, ex, "Research search failed. No search results were accepted.");
            throw new HttpRequestException("Research search unavailable; no keyword measurement can be inferred.");
        }
    }

    private List<SearXngRawResult> CollectResults(JsonElement resultsElement, IReadOnlyCollection<string>? includeDomains, IReadOnlyCollection<string>? excludeDomains)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SearXngRawResult>();
        var excluded = excludeDomains ?? [];

        foreach (var item in resultsElement.EnumerateArray())
        {
            var resultUrl = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
            var host = ExtractHost(resultUrl);

            // Dedupe by URL
            if (!seen.Add(resultUrl)) continue;

            // Domain exclusion
            if (!Uri.TryCreate(resultUrl,UriKind.Absolute,out var validUri) || validUri.Scheme is not ("http" or "https")) continue;
            if (includeDomains is { Count: > 0 } && !includeDomains.Any(d => ResearchEvidenceHandoff.MatchesDomain(resultUrl,d))) continue;
            if (excluded.Any(d => ResearchEvidenceHandoff.MatchesDomain(resultUrl,d))) continue;

            var publishedDate = item.TryGetProperty("publishedDate", out var pd) && pd.ValueKind == JsonValueKind.String
                ? pd.GetString() : null;

            var engines = new List<string>();
            if (item.TryGetProperty("engines", out var enginesProp) && enginesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in enginesProp.EnumerateArray())
                {
                    var name = e.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) engines.Add(name);
                }
            }
            else if (item.TryGetProperty("engine", out var singleEngine) && singleEngine.ValueKind == JsonValueKind.String)
            {
                var name = singleEngine.GetString();
                if (!string.IsNullOrWhiteSpace(name)) engines.Add(name);
            }

            results.Add(new SearXngRawResult
            {
                Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                Url = resultUrl,
                Snippet = item.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "",
                RawScore = item.TryGetProperty("score", out var score) && score.TryGetDecimal(out var parsed) ? parsed : 1m,
                PublishedDate = publishedDate,
                Engines = engines,
                Host = host
            });
        }

        return results;
    }

    private SearXngScoredResult ScoreResult(SearXngRawResult raw, string query)
    {
        var score = raw.RawScore;

        // Domain weighting: authoritative financial/gov sources get a boost
        score += GetDomainWeight(raw.Host);

        // Freshness scoring: newer content ranks higher
        score += GetFreshnessBonus(raw.PublishedDate);

        // Multi-engine corroboration: if multiple engines returned the same URL, boost it
        if (raw.Engines.Count > 1)
        {
            score += raw.Engines.Count * 0.5m;
        }

        // Answer-focused ranking: boost results whose snippet directly addresses the query
        score += GetAnswerRelevanceBonus(raw.Snippet, raw.Title, query);

        // Reddit/forum boost for experience-based queries
        if (QueryWantsForumResults(query) && raw.Host.Contains("reddit.com", StringComparison.OrdinalIgnoreCase))
        {
            score += 2m;
        }

        return new SearXngScoredResult
        {
            Title = raw.Title,
            Url = raw.Url,
            Snippet = raw.Snippet,
            Score = score,
            PublishedDate = raw.PublishedDate,
            Engines = raw.Engines,
            Host = raw.Host
        };
    }

    private async Task EnrichWithExtractedContent(List<SearXngScoredResult> results, CancellationToken ct)
    {
        var tasks = results.Select(async r =>
        {
            if (r.Snippet.Length >= 300) return; // Already has decent content

            try
            {
                var extracted = await _extractor.ExtractAsync(r.Url, ct);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    // Use extracted content but cap it to ~500 chars to match Tavily behavior
                    r.Snippet = extracted.Length > 500
                        ? extracted[..500].TrimEnd() + "..."
                        : extracted;
                }
            }
            catch (Exception ex)
            {
                WorkflowDiagnostics.LogFailure(_logger, ex, "Source extraction failed. Only the search snippet remains; full source text was not collected.", WorkflowDiagnostics.SourceId(r.Url));
            }
        });

        await Task.WhenAll(tasks);
    }

    private static SearXngScoredResult CleanSnippet(SearXngScoredResult result)
    {
        // Strip common SearXNG snippet artifacts
        var snippet = result.Snippet;
        if (string.IsNullOrWhiteSpace(snippet)) return result;

        // Remove leading "..." that SearXNG sometimes prepends
        snippet = snippet.TrimStart('.', ' ', '…');

        // Remove truncated trailing text artifacts
        if (snippet.EndsWith("…", StringComparison.Ordinal) || snippet.EndsWith("...", StringComparison.Ordinal))
        {
            // Find the last complete sentence
            var lastPeriod = snippet.LastIndexOf('.');
            if (lastPeriod > snippet.Length / 2)
            {
                snippet = snippet[..(lastPeriod + 1)].Trim();
            }
        }

        return result with { Snippet = snippet };
    }

    private static bool QueryWantsForumResults(string query)
    {
        var lower = query.ToLowerInvariant();
        var forumSignals = new[] { "people also ask", "reddit", "forum", "experience", "opinion", "review",
            "what do people", "how do you feel", "pain point", "struggle", "frustrated", "overwhelmed",
            "autocase", "autocomplete", "beginner" };
        return forumSignals.Any(s => lower.Contains(s));
    }

    private static decimal GetDomainWeight(string host)
    {
        var lower = host.ToLowerInvariant().TrimStart("www.".ToCharArray());

        // High authority financial/gov/edu domains
        if (lower.EndsWith(".gov")) return 3m;
        if (lower.EndsWith(".edu")) return 2.5m;
        if (lower is "consumer.gov" or "consumerfinance.gov" or "cfpb.gov" or "usa.gov"
            or "nerdwallet.com" or "investopedia.com" or "thebalance.com" or "bankrate.com"
            or "creditkarma.com" or "smartasset.com" or "khanacademy.org") return 2m;

        // Good financial content sources
        if (lower is "forbes.com" or "money.com" or "kiplinger.com" or "morningstar.com"
            or "schwab.com" or "fidelity.com" or "vanguard.com") return 1.5m;

        // Reddit for experiential content
        if (lower.EndsWith("reddit.com")) return 1.5m;

        // StackOverflow/Quora for how-to
        if (lower.EndsWith("stackoverflow.com") || lower.EndsWith("quora.com")) return 1.2m;

        // Medium/personal blogs — neutral
        if (lower.EndsWith("medium.com") || lower.EndsWith("substack.com")) return 0.5m;

        // Thin/low-quality domains
        if (lower.Contains("pinterest") || lower.Contains("tiktok") || lower.Contains("facebook.com")
            || lower.Contains("youtube.com")) return -1m;

        return 0m;
    }

    private static decimal GetFreshnessBonus(string? publishedDate)
    {
        if (string.IsNullOrWhiteSpace(publishedDate)) return 0m;

        if (DateTime.TryParse(publishedDate, out var date))
        {
            var age = DateTime.UtcNow - date;
            if (age.TotalDays < 30) return 2m;       // Last month
            if (age.TotalDays < 90) return 1.5m;      // Last quarter
            if (age.TotalDays < 365) return 1m;       // Last year
            if (age.TotalDays < 730) return 0.5m;      // Last 2 years
            return -0.5m;                              // Older than 2 years
        }

        return 0m;
    }

    private static decimal GetAnswerRelevanceBonus(string snippet, string title, string query)
    {
        if (string.IsNullOrWhiteSpace(snippet) && string.IsNullOrWhiteSpace(title)) return 0m;

        var bonus = 0m;
        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();

        if (queryTerms.Count == 0) return 0m;

        var combinedText = $"{title} {snippet}".ToLowerInvariant();
        var matchCount = queryTerms.Count(term => combinedText.Contains(term));

        // Boost for high query term coverage
        var coverage = (decimal)matchCount / queryTerms.Count;
        if (coverage >= 0.8m) bonus += 2m;
        else if (coverage >= 0.5m) bonus += 1m;
        else if (coverage >= 0.3m) bonus += 0.5m;

        // Boost for how-to / actionable phrasing in the result
        var actionSignals = new[] { "how to", "step by step", "guide", "tips", "ways to", "best way", "simple", "easy" };
        if (actionSignals.Any(s => combinedText.Contains(s))) bonus += 0.5m;

        return bonus;
    }

    private static string ExtractHost(string url)
    {
        try { return new Uri(url).Host; }
        catch { return ""; }
    }

    private record SearXngRawResult
    {
        public string Title { get; init; } = "";
        public string Url { get; init; } = "";
        public string Snippet { get; init; } = "";
        public decimal RawScore { get; init; }
        public string? PublishedDate { get; init; }
        public List<string> Engines { get; init; } = [];
        public string Host { get; init; } = "";
    }

    private record SearXngScoredResult
    {
        public string Title { get; init; } = "";
        public string Url { get; init; } = "";
        public string Snippet { get; set; } = "";
        public decimal Score { get; init; }
        public string? PublishedDate { get; init; }
        public List<string> Engines { get; init; } = [];
        public string Host { get; init; } = "";
    }
}
