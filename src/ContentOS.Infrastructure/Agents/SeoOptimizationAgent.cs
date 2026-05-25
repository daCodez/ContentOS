using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContentOS.Infrastructure.Agents;

public interface ISeoOptimizationAgent
{
    Task<OptimizedSeoPackage> BuildOptimizedSeoPackageAsync(
        ContentIdea idea,
        KeywordStrategyResult keywordStrategy,
        IReadOnlyCollection<string> secondaryKeywords,
        CancellationToken cancellationToken = default);
}

public sealed class SeoOptimizationAgent : ISeoOptimizationAgent
{
    private readonly IResearchSearchClient _searchClient;
    private readonly IWorkflowArticleWriter _writer;
    private readonly ILogger<SeoOptimizationAgent> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SeoOptimizationAgent(
        IResearchSearchClient searchClient,
        IWorkflowArticleWriter writer,
        ILogger<SeoOptimizationAgent> logger)
    {
        _searchClient = searchClient;
        _writer = writer;
        _logger = logger;
    }

    public async Task<OptimizedSeoPackage> BuildOptimizedSeoPackageAsync(
        ContentIdea idea,
        KeywordStrategyResult keywordStrategy,
        IReadOnlyCollection<string> secondaryKeywords,
        CancellationToken cancellationToken = default)
    {
        var primaryKeyword = idea.PrimaryKeyword ?? keywordStrategy.PrimaryKeyword;

        // === Step 1: Title Iteration ===
        var titleCandidates = await GenerateTitleCandidatesAsync(idea, primaryKeyword, cancellationToken);
        var scoredTitles = ScoreTitles(titleCandidates, primaryKeyword);
        var topTitles = scoredTitles.OrderByDescending(t => t.Total).Take(5).ToList();
        var bestTitle = topTitles.FirstOrDefault()?.Title ?? idea.Title;

        _logger.LogInformation(
            "Title iteration: {Count} candidates, top 3 scores: {Scores}",
            titleCandidates.Length,
            string.Join(", ", topTitles.Select(t => $"{t.Title[..Math.Min(30, t.Title.Length)]}={t.Total}")));

        // === Step 2: Meta Description Iteration ===
        var metaCandidates = await GenerateMetaDescriptionsAsync(idea, primaryKeyword, bestTitle, cancellationToken);
        var bestMeta = PickBestMetaDescription(metaCandidates, primaryKeyword);

        _logger.LogInformation(
            "Meta iteration: {Count} candidates, best: {Best}",
            metaCandidates.Length,
            bestMeta?[..Math.Min(60, bestMeta.Length)]);

        // === Step 3: Keyword Expansion from SearXNG ===
        var serpKeywords = await ExpandKeywordsFromSerpAsync(primaryKeyword, cancellationToken);

        // === Step 4: Pattern-based expansion ===
        var patternKeywords = ExpandWithPatterns(primaryKeyword, secondaryKeywords);

        // === Step 5: Cluster keywords ===
        var allKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in keywordStrategy.SecondaryKeywords) allKeywords.Add(k);
        foreach (var k in keywordStrategy.SupportingPhrases) allKeywords.Add(k);
        foreach (var k in serpKeywords) allKeywords.Add(k);
        foreach (var k in patternKeywords) allKeywords.Add(k);
        foreach (var k in secondaryKeywords) allKeywords.Add(k);
        allKeywords.Remove(primaryKeyword); // Remove before re-adding as primary

        var clustered = ClusterKeywords(primaryKeyword, allKeywords.ToList());

        // === Step 6: FAQ generation ===
        var faqQuestions = await GenerateFaqQuestionsAsync(idea, primaryKeyword, clustered.FaqKeywords, cancellationToken);

        // === Step 7: Enforce minimums ===
        var meetsMinimums = clustered.SecondaryKeywords.Count >= 5
            && clustered.SemanticKeywords.Count >= 10
            && clustered.FaqKeywords.Count >= 5
            && faqQuestions.Length >= 5;

        _logger.LogInformation(
            "SEO package: secondary={Sec}, semantic={Sem}, faq={Faq}, questions={Q}, meetsMin={Meets}",
            clustered.SecondaryKeywords.Count,
            clustered.SemanticKeywords.Count,
            clustered.FaqKeywords.Count,
            faqQuestions.Length,
            meetsMinimums);

        var summary = $"Primary: {primaryKeyword} | Secondary: {clustered.SecondaryKeywords.Count} | Semantic: {clustered.SemanticKeywords.Count} | FAQ: {clustered.FaqKeywords.Count} | Titles: {topTitles.Count} | Meta: {(string.IsNullOrWhiteSpace(bestMeta) ? "none" : "selected")}";

        return new OptimizedSeoPackage(
            PrimaryKeyword: primaryKeyword,
            TopTitles: topTitles.Select(t => t.Title).ToArray(),
            BestTitle: bestTitle,
            BestMetaDescription: bestMeta ?? string.Empty,
            MetaDescriptionOptions: metaCandidates,
            SecondaryKeywords: clustered.SecondaryKeywords.ToArray(),
            SemanticKeywords: clustered.SemanticKeywords.ToArray(),
            FaqKeywords: clustered.FaqKeywords.ToArray(),
            FaqQuestions: faqQuestions,
            KeywordClusterSummary: summary,
            MeetsMinimums: meetsMinimums,
            IsQualitySufficient: meetsMinimums && topTitles.Count >= 1 && !string.IsNullOrWhiteSpace(bestMeta));
    }

    // === Title Generation ===

    private async Task<string[]> GenerateTitleCandidatesAsync(ContentIdea idea, string primaryKeyword, CancellationToken ct)
    {
        // Start with existing title options from keyword strategy
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(idea.Title)) candidates.Add(idea.Title.Trim());

        // Generate more via LLM
        try
        {
            var prompt = $"Generate 8 different article title options for the keyword \"{primaryKeyword}\".\n" +
                         $"Pain point: {idea.AudiencePainPoint}\n" +
                         $"Goal: {idea.AudienceGoal}\n" +
                         $"Angle: {idea.RecommendedAngle}\n\n" +
                         "Rules:\n" +
                         "- Each title must include the primary keyword or a close variation\n" +
                         "- Mix types: some with numbers, some with 'how to', some with 'vs', some curiosity-driven\n" +
                         "- Make them specific (include dollar amounts, timeframes, or audience where possible)\n" +
                         "- Each title must be under 70 characters\n" +
                         "- Return JSON: { \"titles\": [\"title1\", \"title2\", ...] }";

            var response = await _writer.GenerateIdeationResponseAsync(prompt, ct);
            if (response?.Ideas != null)
            {
                // The ideation response format won't have titles directly, try parsing raw
            }
        }
        catch { /* LLM not available, use patterns */ }

        // Pattern-based title generation as fallback/supplement
        var patterns = new[]
        {
            $"How to {primaryKeyword} (Step-by-Step Guide)",
            $"{primaryKeyword} for Beginners: Simple System That Works",
            $"Why Most {primaryKeyword} Attempts Fail (And How to Fix It)",
            $"The Truth About {primaryKeyword} (What No One Tells You)",
            $"Best {primaryKeyword} Tools and Apps (Free vs Paid)",
            $"{primaryKeyword} vs Traditional Budgeting (Which Works Better?)",
            $"How to {primaryKeyword} on a Low Income (Real Example)",
            $"{primaryKeyword}: Complete Beginner Roadmap"
        };

        foreach (var p in patterns)
        {
            if (!candidates.Contains(p, StringComparer.OrdinalIgnoreCase))
                candidates.Add(p);
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static TitleScore[] ScoreTitles(string[] titles, string primaryKeyword)
    {
        return titles.Select(t =>
        {
            var lower = t.ToLowerInvariant();
            var kwLower = primaryKeyword.ToLowerInvariant().Trim();

            var keywordPresence = lower.Contains(kwLower) ? 3m :
                kwLower.Split(' ').Any(w => w.Length > 3 && lower.Contains(w)) ? 1.5m : 0m;

            var clickPotential = 0m;
            if (lower.Contains("how to")) clickPotential += 1m;
            if (lower.Contains("best")) clickPotential += 0.5m;
            if (lower.Contains("free")) clickPotential += 0.5m;
            if (lower.Contains("truth") || lower.Contains("no one tells") || lower.Contains("secret")) clickPotential += 1m;
            if (lower.Contains("vs")) clickPotential += 0.75m;
            clickPotential = Math.Min(3m, clickPotential);

            var clarity = t.Length <= 70 ? 3m : t.Length <= 80 ? 2m : 1m;
            if (lower.Contains("?") && !lower.Contains("how")) clarity -= 0.5m; // Questions can be vague

            var specificity = 0m;
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"\$\d")) specificity += 1.5m;
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"\d+")) specificity += 1m;
            if (lower.Contains("for beginners") || lower.Contains("for low income") || lower.Contains("step by step")) specificity += 1m;
            specificity = Math.Min(3m, specificity);

            return new TitleScore(t, keywordPresence, clickPotential, clarity, specificity,
                keywordPresence + clickPotential + clarity + specificity);
        }).ToArray();
    }

    // === Meta Description ===

    private async Task<string[]> GenerateMetaDescriptionsAsync(ContentIdea idea, string primaryKeyword, string bestTitle, CancellationToken ct)
    {
        var candidates = new List<string>();

        // Pattern-based meta descriptions
        var painPoint = string.IsNullOrWhiteSpace(idea.AudiencePainPoint) ? "struggling with budgeting" : idea.AudiencePainPoint.Trim();
        var goal = string.IsNullOrWhiteSpace(idea.AudienceGoal) ? "take control of your money" : idea.AudienceGoal.Trim();

        var templates = new[]
        {
            $"Struggling with {primaryKeyword}? {(painPoint.Length > 0 ? char.ToUpper(painPoint[0]) + painPoint[1..].TrimEnd('.') : painPoint)} — here's a simple system that actually works. Start today.",
            $"Learn {primaryKeyword} with a practical step-by-step plan. {goal.TrimEnd('.')} — even if you've failed before.",
            $"The honest guide to {primaryKeyword}. No jargon, no judgment. Just a clear path to {goal.TrimEnd('.').ToLowerInvariant()}.",
            $"{bestTitle}. A practical breakdown with real examples, free tools, and a step-by-step system anyone can follow.",
            $"Stop guessing and start {primaryKeyword}. This guide covers common mistakes, the best free tools, and a simple plan you can start today."
        };

        candidates.AddRange(templates);

        return candidates
            .Where(m => m.Length >= 120 && m.Length <= 160)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? PickBestMetaDescription(string[] candidates, string primaryKeyword)
    {
        var kwLower = primaryKeyword.ToLowerInvariant();

        return candidates
            .Select(m => new { Meta = m, Score = ScoreMeta(m, kwLower) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault()?.Meta;
    }

    private static decimal ScoreMeta(string meta, string kwLower)
    {
        var score = 0m;
        var lower = meta.ToLowerInvariant();

        if (lower.Contains(kwLower)) score += 2m;
        else if (kwLower.Split(' ').Any(w => w.Length > 3 && lower.Contains(w))) score += 1m;

        if (lower.Contains("step-by-step") || lower.Contains("simple") || lower.Contains("practical")) score += 1m;
        if (lower.Contains("free") || lower.Contains("tools") || lower.Contains("today")) score += 0.5m;
        if (lower.Contains("?") == false && lower.Contains("—")) score += 0.5m; // Action-oriented

        // Prefer 140-160 chars
        if (meta.Length >= 140 && meta.Length <= 160) score += 1m;
        else if (meta.Length >= 120) score += 0.5m;

        return score;
    }

    // === Keyword Expansion from SERP ===

    private async Task<List<string>> ExpandKeywordsFromSerpAsync(string primaryKeyword, CancellationToken ct)
    {
        var keywords = new List<string>();

        try
        {
            // Search for the primary keyword to find related terms in SERP
            var results = await _searchClient.SearchAsync(
                $"{primaryKeyword} for beginners how to",
                maxResults: 8,
                cancellationToken: ct);

            foreach (var result in results)
            {
                // Extract keywords from titles
                if (!string.IsNullOrWhiteSpace(result.Title))
                {
                    var titleKws = ExtractKeywordsFromText(result.Title, primaryKeyword);
                    keywords.AddRange(titleKws);
                }

                // Extract keywords from snippets
                if (!string.IsNullOrWhiteSpace(result.Content))
                {
                    var snippetKws = ExtractKeywordsFromText(result.Content, primaryKeyword);
                    keywords.AddRange(snippetKws);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SERP keyword expansion failed for '{Keyword}'.", primaryKeyword);
        }

        return keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractKeywordsFromText(string text, string primaryKeyword)
    {
        var keywords = new List<string>();
        var lower = text.ToLowerInvariant();

        // Extract phrases that contain parts of the primary keyword
        var kwParts = primaryKeyword.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToArray();

        // Look for "how to X" patterns
        var howToMatch = System.Text.RegularExpressions.Regex.Match(lower, @"how to ([^.!?]{10,50})");
        if (howToMatch.Success)
            keywords.Add("how to " + howToMatch.Groups[1].Value.Trim());

        // Look for "best X" patterns
        var bestMatch = System.Text.RegularExpressions.Regex.Match(lower, @"best ([^.!?]{5,40})");
        if (bestMatch.Success)
            keywords.Add("best " + bestMatch.Groups[1].Value.Trim());

        // Look for "X vs Y" patterns
        var vsMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\w[\w ]{3,25}) vs (\w[\w ]{3,25})");
        if (vsMatch.Success)
            keywords.Add(vsMatch.Value.Trim());

        // Look for "X for beginners/simple/easy" patterns
        foreach (var suffix in new[] { "for beginners", "simple", "easy", "step by step", "free" })
        {
            if (lower.Contains(suffix))
            {
                var phrase = $"{primaryKeyword} {suffix}";
                keywords.Add(phrase);
            }
        }

        return keywords;
    }

    // === Pattern-based Keyword Expansion ===

    private static List<string> ExpandWithPatterns(string primaryKeyword, IReadOnlyCollection<string> existing)
    {
        var keywords = new List<string>();
        var kw = primaryKeyword.Trim().ToLowerInvariant();
        var existingLower = existing.Select(e => e.ToLowerInvariant().Trim()).ToHashSet();

        var patterns = new[]
        {
            $"how to {kw}",
            $"best {kw} apps",
            $"best {kw} tools",
            $"{kw} for beginners",
            $"{kw} step by step",
            $"{kw} for low income",
            $"{kw} for families",
            $"{kw} vs traditional",
            $"{kw} spreadsheet template",
            $"{kw} mistakes to avoid",
            $"simple {kw} system",
            $"{kw} with bad credit",
            $"{kw} with irregular income",
            $"{kw} on one income",
            $"free {kw} template",
            $"{kw} apps free",
            $"{kw} examples",
            $"{kw} categories"
        };

        foreach (var p in patterns)
        {
            if (!existingLower.Contains(p) && !p.Equals(kw, StringComparison.OrdinalIgnoreCase))
                keywords.Add(p);
        }

        return keywords;
    }

    // === Keyword Clustering ===

    private static (List<string> SecondaryKeywords, List<string> SemanticKeywords, List<string> FaqKeywords) ClusterKeywords(
        string primaryKeyword, List<string> allKeywords)
    {
        var secondary = new List<string>();
        var semantic = new List<string>();
        var faq = new List<string>();

        var kwLower = primaryKeyword.ToLowerInvariant().Trim();

        foreach (var keyword in allKeywords.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var kLower = keyword.ToLowerInvariant().Trim();
            if (kLower == kwLower) continue;

            // FAQ keywords: question-words, "how", "what", "why", "can you"
            if (kLower.StartsWith("how ") || kLower.StartsWith("what ") || kLower.StartsWith("why ") ||
                kLower.StartsWith("can ") || kLower.StartsWith("when ") || kLower.StartsWith("is ") ||
                kLower.Contains("mistakes") || kLower.Contains("avoid") || kLower.Contains("difference between"))
            {
                faq.Add(keyword);
            }
            // Secondary: contains primary keyword or major parts
            else if (kLower.Contains(kwLower) || kwLower.Split(' ').Any(w => w.Length > 3 && kLower.Contains(w)))
            {
                secondary.Add(keyword);
            }
            // Semantic: related but not containing the primary keyword directly
            else
            {
                semantic.Add(keyword);
            }
        }

        // Enforce minimums by promoting from semantic if needed
        while (secondary.Count < 5 && semantic.Count > 0)
        {
            secondary.Add(semantic[0]);
            semantic.RemoveAt(0);
        }

        while (faq.Count < 3 && secondary.Count > 5)
        {
            var promoted = secondary.Last();
            faq.Add($"what is {promoted}");
            secondary.Remove(promoted);
        }

        return (secondary.Take(10).ToList(), semantic.Take(20).ToList(), faq.Take(10).ToList());
    }

    // === FAQ Generation ===

    private async Task<string[]> GenerateFaqQuestionsAsync(ContentIdea idea, string primaryKeyword, IReadOnlyCollection<string> faqKeywords, CancellationToken ct)
    {
        var questions = new List<string>
        {
            $"What is the best way to start {primaryKeyword}?",
            $"How do you make {primaryKeyword} work in real life?",
            $"What mistakes should you avoid with {primaryKeyword}?"
        };

        // Generate from FAQ keywords
        foreach (var faqKw in faqKeywords.Take(4))
        {
            var kwVal = faqKw;
            var lower = kwVal.ToLowerInvariant();
            if (lower.StartsWith("how ") || lower.StartsWith("what ") || lower.StartsWith("why ") || lower.StartsWith("can "))
            {
                if (!lower.EndsWith("?")) kwVal += "?";
                questions.Add(kwVal.Length > 0 ? char.ToUpper(kwVal[0]) + kwVal[1..] : kwVal);
            }
            else
            {
                questions.Add($"How does {kwVal} work in practice?");
            }
        }

        return questions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}