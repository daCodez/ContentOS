using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Ideation;

public interface ITopicDiversityScorer
{
    Task ClassifyAndScoreAsync(IList<CandidateContentIdea> ideas, TopicDiversityConfig config, CancellationToken ct = default);
    List<CandidateContentIdea> EnforceDistribution(List<CandidateContentIdea> ideas, TopicDiversityConfig config);
    List<CandidateContentIdea> GuardDuplicatePatterns(List<CandidateContentIdea> ideas, TopicDiversityConfig config);
    List<CandidateContentIdea> FilterUnviable(List<CandidateContentIdea> ideas, TopicDiversityConfig config);
}

public sealed class TopicDiversityScorer : ITopicDiversityScorer
{
    private readonly IResearchSearchClient _searchClient;
    private readonly ILogger<TopicDiversityScorer> _logger;

    public TopicDiversityScorer(IResearchSearchClient searchClient, ILogger<TopicDiversityScorer> logger)
    {
        _searchClient = searchClient;
        _logger = logger;
    }

    public async Task ClassifyAndScoreAsync(IList<CandidateContentIdea> ideas, TopicDiversityConfig config, CancellationToken ct = default)
    {
        // Phase 1: Classify, detect specificity, score base metrics (synchronous)
        foreach (var idea in ideas)
        {
            idea.TopicType = ClassifyType(idea);
            idea.TopicTypeLabel = idea.TopicType.ToLabel();
            idea.SpecificityTag = DetectSpecificity(idea);
            idea.IntentMatchScore = ScoreIntentMatch(idea);
            idea.UniquenessScore = ScoreUniqueness(idea, ideas);
            idea.ClickPotentialScore = ScoreClickPotential(idea);
            idea.MonetizationPotentialScore = ScoreMonetizationPotential(idea);
            idea.LowCompetitionBoost = ScoreLowCompetitionBoost(idea, config);
        }

        // Phase 2: SERP difficulty check (async — one search per keyword)
        await CheckSerpDifficultyAsync(ideas, config, ct);

        // Phase 3: Monetization viability check
        CheckMonetizationViability(ideas, config);

        // Phase 4: Compute final scores
        foreach (var idea in ideas)
        {
            var baseScore = idea.IntentMatchScore + idea.UniquenessScore + idea.ClickPotentialScore + idea.MonetizationPotentialScore;
            var adjustedScore = baseScore * idea.CompetitionModifier + idea.LowCompetitionBoost;

            if (!idea.MonetizationViable)
                adjustedScore -= 2m;

            idea.OverallScore = Math.Round(Math.Max(0m, adjustedScore), 1);
            idea.SeoOpportunityScore = idea.IntentMatchScore * 25m * idea.CompetitionModifier;
            idea.MonetizationFitScore = (idea.MonetizationPotentialScore + idea.LowCompetitionBoost) * 25m;
            idea.AudienceFitScore = idea.ClickPotentialScore * 25m;
            idea.CompetitionDifficultyScore = idea.IsHighCompetition ? 80m : (10m - idea.UniquenessScore) * 10m;
        }
    }

    // --- SERP difficulty filter ---

    private async Task CheckSerpDifficultyAsync(IList<CandidateContentIdea> ideas, TopicDiversityConfig config, CancellationToken ct)
    {
        var keywords = ideas
            .Where(i => !string.IsNullOrWhiteSpace(i.PrimaryKeyword))
            .Select(i => i.PrimaryKeyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15) // Cap to avoid hammering SearXNG
            .ToList();

        if (keywords.Count == 0) return;

        var keywordDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords)
        {
            try
            {
                var results = await _searchClient.SearchAsync(keyword, maxResults: 5, cancellationToken: ct);
                var domains = results
                    .Where(r => !string.IsNullOrWhiteSpace(r.Url))
                    .Select(r => ExtractDomain(r.Url))
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                keywordDomains[keyword] = domains;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SERP difficulty check failed for keyword '{Keyword}'. Skipping.", keyword);
                keywordDomains[keyword] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        foreach (var idea in ideas)
        {
            if (!keywordDomains.TryGetValue(idea.PrimaryKeyword.Trim(), out var domains)) continue;

            idea.SerpChecked = true;
            idea.AuthorityDomainsInSerp = domains.Count(d => config.AuthorityDomains.Contains(d));
            idea.IsHighCompetition = idea.AuthorityDomainsInSerp >= config.MaxAuthorityDomainsInSerp;

            if (idea.IsHighCompetition)
            {
                idea.CompetitionModifier = 0.5m;
                _logger.LogInformation(
                    "High-competition keyword '{Keyword}': {Authority}/{Total} authority domains in top SERP. Score halved.",
                    idea.PrimaryKeyword, idea.AuthorityDomainsInSerp, domains.Count);
            }
            else if (idea.AuthorityDomainsInSerp >= 2)
            {
                idea.CompetitionModifier = 0.75m;
                _logger.LogInformation(
                    "Moderate-competition keyword '{Keyword}': {Authority}/{Total} authority domains. Score reduced 25%.",
                    idea.PrimaryKeyword, idea.AuthorityDomainsInSerp, domains.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Low-competition keyword '{Keyword}': {Authority}/{Total} authority domains. Full score.",
                    idea.PrimaryKeyword, idea.AuthorityDomainsInSerp, domains.Count);
            }
        }
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            var host = new Uri(url).Host.ToLowerInvariant();
            return host.StartsWith("www.") ? host[4..] : host;
        }
        catch { return string.Empty; }
    }

    // --- Low-competition modifier ---

    private static decimal ScoreLowCompetitionBoost(CandidateContentIdea idea, TopicDiversityConfig config)
    {
        var combined = (idea.Title + " " + idea.RecommendedAngle).ToLowerInvariant();
        var matchCount = config.LowCompetitionSignals.Count(s => combined.Contains(s, StringComparison.OrdinalIgnoreCase));

        var boost = matchCount switch
        {
            >= 3 => 1.5m,
            2 => 1.0m,
            1 => 0.5m,
            _ => 0m
        };

        return boost;
    }

    // --- Monetization viability check ---

    private void CheckMonetizationViability(IList<CandidateContentIdea> ideas, TopicDiversityConfig config)
    {
        foreach (var idea in ideas)
        {
            var combined = (idea.Title + " " + idea.RecommendedAngle + " " + idea.AudienceGoal).ToLowerInvariant();

            // Check if this is a purely informational/definitional topic with no natural product path
            var isHardToMonetize = config.HardToMonetizeSignals.Any(s => combined.Contains(s, StringComparison.OrdinalIgnoreCase));

            // Check for any monetization signals
            var hasMonetizationPath =
                idea.TopicType == TopicType.Comparison ||  // Comparisons = affiliate
                idea.TopicType == TopicType.Solution ||    // Solutions = tool recommendations
                idea.MonetizationPotentialScore >= 1.0m || // Already scored as monetizable
                combined.Contains("app", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("template", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("software", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("service", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("course", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("book", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("budget", StringComparison.OrdinalIgnoreCase); // Budget content almost always monetizable

            if (isHardToMonetize && !hasMonetizationPath)
            {
                idea.MonetizationViable = false;
                idea.MonetizationWarning = "No natural monetization path — topic is purely informational/definitional. Consider adding tool recommendations or rewriting with a practical angle.";
                _logger.LogInformation("Monetization warning for '{Title}': {Warning}", idea.Title, idea.MonetizationWarning);
            }
            else if (!hasMonetizationPath && idea.MonetizationPotentialScore < 0.75m)
            {
                idea.MonetizationWarning = "Weak monetization path. Consider adding a tools section or affiliate angle.";
                _logger.LogDebug("Weak monetization for '{Title}': score={Score}", idea.Title, idea.MonetizationPotentialScore);
            }
        }
    }

    // --- Filter unviable topics ---

    public List<CandidateContentIdea> FilterUnviable(List<CandidateContentIdea> ideas, TopicDiversityConfig config)
    {
        var result = new List<CandidateContentIdea>();

        foreach (var idea in ideas)
        {
            // Hard skip: high competition AND no low-competition signals
            if (idea.IsHighCompetition && idea.LowCompetitionBoost <= 0m)
            {
                _logger.LogInformation(
                    "Dropped '{Title}' — high competition with no low-competition signals.",
                    idea.Title);
                continue;
            }

            // Hard skip: not monetization viable AND low overall score
            if (!idea.MonetizationViable && idea.OverallScore < config.MinScoreToKeep)
            {
                _logger.LogInformation(
                    "Dropped '{Title}' — no monetization path and score {Score} below threshold {Min}.",
                    idea.Title, idea.OverallScore, config.MinScoreToKeep);
                continue;
            }

            result.Add(idea);
        }

        _logger.LogInformation(
            "Viability filter: {Kept}/{Total} ideas kept. Dropped {Dropped}.",
            result.Count, ideas.Count, ideas.Count - result.Count);

        return result;
    }

    // --- Distribution enforcement ---

    public List<CandidateContentIdea> EnforceDistribution(List<CandidateContentIdea> ideas, TopicDiversityConfig config)
    {
        var clusterSize = config.ClusterSize;
        var minPerType = config.MinPerType;
        var result = new List<CandidateContentIdea>();
        var usedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var byType = ideas
            .OrderByDescending(i => i.OverallScore)
            .GroupBy(i => i.TopicType)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (TopicType type in Enum.GetValues<TopicType>())
        {
            if (!byType.TryGetValue(type, out var candidates) || candidates.Count == 0)
            {
                _logger.LogWarning("No candidates for topic type {Type}.", type.ToLabel());
                continue;
            }

            var taken = 0;
            foreach (var candidate in candidates)
            {
                if (taken >= minPerType) break;
                if (usedTitles.Contains(candidate.Title)) continue;
                result.Add(candidate);
                usedTitles.Add(candidate.Title);
                taken++;
            }

            if (taken < minPerType)
                _logger.LogWarning("Only {Taken}/{Min} candidates for type {Type}.", taken, minPerType, type.ToLabel());
        }

        var remaining = ideas
            .Where(i => !usedTitles.Contains(i.Title))
            .OrderByDescending(i => i.OverallScore)
            .Take(clusterSize - result.Count);

        result.AddRange(remaining);

        _logger.LogInformation(
            "Distribution: {Total} ideas, {Types} types. {Breakdown}",
            result.Count,
            result.Select(i => i.TopicType).Distinct().Count(),
            string.Join(", ", result.GroupBy(i => i.TopicType).Select(g => $"{g.Key.ToLabel()}={g.Count()}")));

        return result;
    }

    // --- Pattern guard ---

    public List<CandidateContentIdea> GuardDuplicatePatterns(List<CandidateContentIdea> ideas, TopicDiversityConfig config)
    {
        var blocked = config.BlockedPatterns;
        var seenPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CandidateContentIdea>();

        foreach (var idea in ideas)
        {
            var titleLower = idea.Title.ToLowerInvariant();

            if (blocked.Any(p => titleLower.Contains(p.ToLowerInvariant())))
            {
                _logger.LogInformation("Rejected '{Title}' — blocked pattern.", idea.Title);
                continue;
            }

            var pattern = ExtractPattern(titleLower);
            if (!seenPatterns.Add(pattern))
            {
                _logger.LogInformation("Rejected '{Title}' — duplicate pattern '{Pattern}'.", idea.Title, pattern);
                continue;
            }

            result.Add(idea);
        }

        return result;
    }

    // --- Classification ---

    private static TopicType ClassifyType(CandidateContentIdea idea)
    {
        var combined = (idea.Title + " " + (idea.RecommendedAngle ?? "")).ToLowerInvariant();

        if (ContainsAny(combined, "truth about", "what no one", "i tried", "results", "secret", "myth", "no one tells", "shocking", "surprising"))
            return TopicType.CTR;

        if (ContainsAny(combined, " vs ", " versus ", "compared", "which is better", "difference between", " or ") &&
            !combined.Contains("or more", StringComparison.OrdinalIgnoreCase))
            return TopicType.Comparison;

        if (ContainsAny(combined, "how to save", "how to build", "how to get out of", "how to stop", "pay off", "get out of debt", "how to reach", "how to achieve"))
            return TopicType.Outcome;

        if (ContainsAny(combined, "$", "family of", "single mom", "one income", "irregular income", "/month", "a month", "per month", "with variable", "on minimum", "low income")
            || System.Text.RegularExpressions.Regex.IsMatch(combined, @"\d{3,}"))
            return TopicType.Scenario;

        if (ContainsAny(combined, "best app", "best tool", "app for", "spreadsheet", "template", "printable", "how to start", "step by step", "system for", "method for", "app review"))
            return TopicType.Solution;

        if (ContainsAny(combined, "ultimate guide", "complete guide", "everything you need", "beginner roadmap", "comprehensive", "all you need to know", "complete beginner"))
            return TopicType.Authority;

        if (ContainsAny(combined, "why", "fails", "mistakes", "wrong", "stuck", "struggling", "trouble", "problem", "pitfalls", "avoid"))
            return TopicType.Problem;

        if (combined.Contains("how to", StringComparison.OrdinalIgnoreCase))
            return TopicType.Solution;

        return TopicType.Problem;
    }

    private static bool ContainsAny(string text, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // --- Specificity ---

    private static string DetectSpecificity(CandidateContentIdea idea)
    {
        var titleLower = idea.Title.ToLowerInvariant();
        var tags = new List<string>();

        if (System.Text.RegularExpressions.Regex.IsMatch(titleLower, @"\$\d")) tags.Add("dollar-amount");
        if (ContainsAny(titleLower, "family", "single mom", "couples", "low income", "beginner", "students", "bad credit")) tags.Add("audience-specific");
        if (ContainsAny(titleLower, "app", "tool", "spreadsheet", "template", "printable", "ynab", "everydollar")) tags.Add("tool-based");
        if (ContainsAny(titleLower, " vs ", " versus ", " or ")) tags.Add("comparison");
        if (ContainsAny(titleLower, "save", "pay off", "get out of", "build", "stop", "start")) tags.Add("outcome-driven");
        if (System.Text.RegularExpressions.Regex.IsMatch(titleLower, @"\d+")) tags.Add("number-specific");

        return tags.Count > 0 ? string.Join("+", tags) : "general";
    }

    // --- Scoring ---

    private static decimal ScoreIntentMatch(CandidateContentIdea idea)
    {
        var score = 1m;
        if (!string.IsNullOrWhiteSpace(idea.PrimaryKeyword)) score += 0.5m;
        if (!string.IsNullOrWhiteSpace(idea.SearchIntent)) score += 0.5m;
        if (!string.IsNullOrWhiteSpace(idea.AudiencePainPoint)) score += 0.5m;
        if (!string.IsNullOrWhiteSpace(idea.AudienceGoal)) score += 0.5m;

        if (idea.TopicType == TopicType.Comparison && idea.Title.Contains(" vs ", StringComparison.OrdinalIgnoreCase)) score += 0.5m;
        if (idea.TopicType == TopicType.Outcome && idea.Title.Contains("how to", StringComparison.OrdinalIgnoreCase)) score += 0.5m;
        if (idea.TopicType == TopicType.Scenario && System.Text.RegularExpressions.Regex.IsMatch(idea.Title, @"\d")) score += 0.5m;

        return Math.Min(3m, score);
    }

    private static decimal ScoreUniqueness(CandidateContentIdea idea, IList<CandidateContentIdea> allIdeas)
    {
        var score = 2m;
        var titleWords = idea.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 3)
            .ToHashSet();

        var similarCount = allIdeas.Count(other =>
            other != idea &&
            other.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .Where(w => w.Length > 3)
                .Intersect(titleWords).Count() >= 3);

        if (similarCount >= 3) score -= 1.5m;
        else if (similarCount >= 2) score -= 1m;
        else if (similarCount >= 1) score -= 0.5m;

        if (idea.SpecificityTag != "general") score += 0.5m;

        var typeCounts = allIdeas.GroupBy(i => i.TopicType).ToDictionary(g => g.Key, g => g.Count());
        if (typeCounts.TryGetValue(idea.TopicType, out var count) && count <= 2)
            score += 0.5m;

        return Math.Clamp(score, 0m, 3m);
    }

    private static decimal ScoreClickPotential(CandidateContentIdea idea)
    {
        var score = 1m;
        var titleLower = idea.Title.ToLowerInvariant();

        if (ContainsAny(titleLower, "how to", "best", "free", "simple", "easy", "step-by-step")) score += 0.5m;
        if (idea.TopicType == TopicType.CTR) score += 0.5m;
        if (idea.TopicType == TopicType.Comparison) score += 0.5m;
        if (idea.TopicType == TopicType.Scenario) score += 0.25m;
        if (idea.SpecificityTag != "general") score += 0.25m;

        return Math.Min(2m, score);
    }

    private static decimal ScoreMonetizationPotential(CandidateContentIdea idea)
    {
        var score = 0.5m;
        var titleLower = idea.Title.ToLowerInvariant();

        if (ContainsAny(titleLower, "app", "tool", "template", "spreadsheet", "printable")) score += 0.5m;
        if (idea.TopicType == TopicType.Comparison) score += 0.5m;
        if (idea.TopicType == TopicType.Solution) score += 0.25m;

        if (!string.IsNullOrWhiteSpace(idea.RecommendedAngle) &&
            idea.RecommendedAngle.Contains("tool", StringComparison.OrdinalIgnoreCase))
            score += 0.25m;

        return Math.Min(2m, score);
    }

    // --- Pattern extraction ---

    private static string ExtractPattern(string titleLower)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(titleLower, @"\$\d[\d,]*", "$AMT");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b\d+\b", "N");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(family of \d|single mom|low income|beginners?)\b", "AUDIENCE");

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var structuralWords = words
            .Where(w => w.Length > 3 || w is "vs" or "for" or "how" or "why" or "the" or "get" or "keep")
            .Take(6)
            .ToArray();

        return string.Join(" ", structuralWords);
    }
}