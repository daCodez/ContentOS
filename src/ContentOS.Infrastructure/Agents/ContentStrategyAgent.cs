using ContentOS.Application.Abstractions;
using ContentOS.Application.Configuration;
using ContentOS.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace ContentOS.Infrastructure.Agents;

public interface IContentStrategyAgent
{
    Task<ResearchIntentResult> BuildResearchIntentAsync(ContentIdea idea, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default);
    Task<KeywordStrategyResult> BuildKeywordStrategyAsync(ContentIdea idea, IReadOnlyCollection<string> secondaryKeywords, CancellationToken cancellationToken = default);
    Task<TopicExpansionResult> BuildTopicExpansionAsync(ContentIdea idea, KeywordStrategyResult keywordStrategy, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default);
    Task<ContentBriefResult> BuildContentBriefAsync(ContentIdea idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default);
    Task<ArticleOutlineResult> CreateArticleOutlineAsync(ContentIdea idea, GeneratedLongformArticle article, TopicExpansionResult topicExpansion, CancellationToken cancellationToken = default);

    // --- NEW: TITLE REFINEMENT ---
    Task<string> RefineAndSelectBestTitleAsync(ContentIdea idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, CancellationToken cancellationToken = default);
}

public sealed class ContentStrategyAgent : IContentStrategyAgent
{
    private readonly ILogger<ContentStrategyAgent> _logger;
    private readonly ILlmClient _llmClient;
    private readonly SimulationSettings _simSettings;

    public ContentStrategyAgent(
        ILogger<ContentStrategyAgent> logger,
        ILlmClient llmClient,
        IOptions<SimulationSettings> simSettings)
    {
        _logger = logger;
        _llmClient = llmClient;
        _simSettings = simSettings.Value;
    }

    // --- NEW HELPERS FOR TITLE/KEYWORD QUALITY (shared with IdeationAgent) ---
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "been", "by", "for", "from", "has", "have", "he", "in", "is", "it", "its", "of", "on", "that", "the", "to", "was", "were", "will", "with"
    };

    private static readonly HashSet<string> BannedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "reddit", "youtube", "quora", "facebook", "twitter", "instagram", "tiktok", "pinterest", "linkedin", "google", "bing", "amazon", "ebay", "walmart", "target", "site", "forum", "blog"
    };

    private static string NormalizeForComparison(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lowered = input.ToLowerInvariant();
        var noPunct = Regex.Replace(lowered, @"\p{P}", " ");
        var words = noPunct.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var filtered = words.Where(w => !StopWords.Contains(w) && w.Length > 1);
        return string.Join(" ", filtered);
    }

    private static string CleanTitleAndKeyword(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var cleaned = input.Trim();
        // Remove trailing " - Source" or " | SiteName"
        cleaned = Regex.Replace(cleaned, @"\s*[-|]\s*\w+$", "");
        // Remove trailing "(Source)"
        cleaned = Regex.Replace(cleaned, @"\s*\([^)]*\)$", "");
        // Remove trailing URL
        cleaned = Regex.Replace(cleaned, @"https?://[^\s]+$", "");
        return cleaned.TrimEnd('-', '|', '(', ' ', ')');
    }

    // --- ENHANCED KEYWORD RULES ---
    private static string NormalizePrimaryKeyword(string? primaryKeyword, string? title)
    {
        var candidate = NormalizeKeywordPhrase(string.IsNullOrWhiteSpace(primaryKeyword) ? title : primaryKeyword);
        if (!IsNaturalKeyword(candidate))
        {
            candidate = NormalizeKeywordPhrase(title);
        }

        if (!IsNaturalKeyword(candidate))
        {
            var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5);
            candidate = string.Join(' ', words);
        }

        return candidate;
    }

    private static string NormalizeKeywordPhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;

        var normalized = phrase.Trim();
        normalized = normalized.Replace(':', ' ').Replace("  ", " ");
        normalized = normalized.StartsWith("how to ", StringComparison.OrdinalIgnoreCase)
            ? normalized[7..].Trim()
            : normalized;
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    private static bool IsNaturalKeyword(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return false;
        var normalized = NormalizeKeywordPhrase(phrase);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 5) return false;
        if (normalized.Contains("$", StringComparison.Ordinal)) return false;
        if (normalized.Contains("actually works", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Contains("simple plan", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Contains("without feeling overwhelmed", StringComparison.OrdinalIgnoreCase)) return false;

        // NEW: Length 2-5 words, no banned platforms, no likely brands
        if (words.Any(w => BannedPlatforms.Contains(w.ToLowerInvariant()))) return false;

        // Reject single-word keywords that are likely brands (unless known generic)
        if (words.Length == 1)
        {
            var generics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "budget", "save", "debt", "income", "expense", "bank", "credit", "loan", "invest", "retirement", "tax", "bill", "money", "cash", "fund" };
            if (!generics.Contains(normalized.ToLowerInvariant())) return false;
        }

        // Reject if contains a 4-digit year (unless we had historical context, but we apply generally)
        if (Regex.IsMatch(normalized, @"\b\d{4}\b")) return false;

        return words.All(w => w.Length <= 20 && char.IsLetterOrDigit(w[0]));
    }

    // --- TITLE REFINEMENT ---
    public async Task<string> RefineAndSelectBestTitleAsync(ContentIdea idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, CancellationToken cancellationToken = default)
    {
        if (_simSettings.UseSimulatedAgents)
        {
            // Gated mock: return a simple variation of the original title
            return $"How to {idea.PrimaryKeyword} for Beginners";
        }

        var prompt = $$"""
        You are an expert Title Editor. Your job is to generate 5 compelling, unique title variations for a blog article based on the core idea, then select the single best one.

        CORE IDEA:
        - Target Reader Problem: {{idea.AudiencePainPoint}}
        - Primary Outcome: {{idea.AudienceGoal}}
        - Recommended Angle: {{idea.RecommendedAngle}}
        - Primary Keyword: {{idea.PrimaryKeyword}}
        - Why Now: {{idea.WhyNow}}

        YOUR TASK:
        1. Generate 5 distinct title variations. Each must:
            - Be between 8 and 14 words long.
            - Clearly communicate the problem, outcome, or angle.
            - Avoid vagueness (no "guide", "tips", "ways" as the main focus unless paired with specificity).
            - Use strong verbs and emotional triggers where appropriate.
            - NOT contain the source, site name, or URL.
            - NOT be a duplicate of each other.
        2. Score each title on:
            - Clarity (0-5): Is the promise clear?
            - Specificity (0-5): Does it include numbers, specific outcomes, or clear audience?
            - Emotional Trigger (0-5): Does it evoke curiosity, urgency, or relief?
            - Clickability (0-5): Would you click this?
            - Length Penalty: -1 if outside 8-14 words.
        3. Return ONLY the single best title (the highest-scoring one) as a plain string.
        Do not number the list. Do not add quotes. Do not add any explanation.
        Just output the winning title on its own line.
        """;

        try
        {
            var titlesText = await _llmClient.GenerateAsync<string>(prompt, model: "gemma4:31b-cloud", cancellationToken);
            var lines = titlesText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .Where(t => t.Length > 0)
                                .ToList();

            if (lines.Count == 0)
            {
                _logger.LogWarning("Title refinement returned no valid titles. Falling back to idea title.");
                return idea.Title;
            }

            // Simple heuristic scoring if we got multiple lines
            if (lines.Count > 1)
            {
                var scoredTitles = new List<(string title, int score)>();
                foreach (var title in lines)
                {
                    int score = 0;
                    // Length: 8-14 words is ideal
                    int wordCount = title.Split(' ').Length;
                    if (wordCount >= 8 && wordCount <= 14) score += 2;
                    else if (wordCount >= 6 && wordCount <= 16) score += 1;
                    else score -= 1; // Penalty for too short/long

                    // Specificity: contains a number or specific outcome
                    if (Regex.IsMatch(title, @"\d+")) score += 2;
                    if (title.Contains("first ") || title.Contains("7 days") || title.Contains("step by step")) score += 1;

                    // Emotional Trigger / Curiosity
                    if (title.Contains("(") && title.Contains(")")) score += 2; // e.g., "(And How to Fix It)"
                    if (title.Contains("Stop ") || title.Contains("Avoid ") || title.Contains("Never ")) score += 1;
                    if (title.Contains("Truth About") || title.Contains("Nobody Tells You") || title.Contains("Secret")) score += 1;

                    // Clarity: contains pain point or goal keywords (simple check)
                    var lowerTitle = title.ToLowerInvariant();
                    if (idea.AudiencePainPoint.Split(' ').Any(w => w.Length > 4 && lowerTitle.Contains(w.ToLowerInvariant()))) score += 1;
                    if (idea.AudienceGoal.Split(' ').Any(w => w.Length > 4 && lowerTitle.Contains(w.ToLowerInvariant()))) score += 1;

                    scoredTitles.Add((title, score));
                }

                var best = scoredTitles.OrderByDescending(t => t.score).ThenBy(t => t.title.Length).First();
                _logger.LogInformation("Selected best title: '{Title}' (score: {Score})", best.title, best.score);
                return best.title;
            }

            // If only one title was returned, use it
            return lines[0].Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Title refinement LLM call failed. Falling back to idea title.");
            return idea.Title; // Fallback to original title on error
        }
    }

    // --- UPDATED BUILDER METHODS ---
    public async Task<ResearchIntentResult> BuildResearchIntentAsync(ContentIdea idea, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default)
    {
        // Clean inputs to strip sources
        var cleanTitle = CleanTitleAndKeyword(idea.Title);
        var cleanKeyword = CleanTitleAndKeyword(idea.PrimaryKeyword);
        var cleanSourceEvidence = sourceSummaries.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => CleanTitleAndKeyword(x.Trim())).Take(5).ToArray();

        if (_simSettings.UseSimulatedAgents)
        {
            var topic = Safe(cleanTitle);
            var keyword = Safe(cleanKeyword);
            var intent = Safe(idea.SearchIntent);
            return new ResearchIntentResult(
                topic,
                keyword,
                intent,
                Safe(idea.RecommendedAngle),
                Safe(idea.AudiencePainPoint),
                Safe(idea.AudienceGoal),
                Safe(idea.WhyNow),
                cleanSourceEvidence,
                !string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(keyword) && !string.IsNullOrWhiteSpace(intent));
        }

        var prompt = $$"""
        You are an expert Content Strategist. Analyze the following idea and source summaries to produce a structured research intent.
        Idea Title: {{cleanTitle}}
        Primary Keyword: {{cleanKeyword}}
        Search Intent: {{idea.SearchIntent}}
        Recommended Angle: {{idea.RecommendedAngle}}
        Audience Pain Point: {{idea.AudiencePainPoint}}
        Audience Goal: {{idea.AudienceGoal}}
        Why Now: {{idea.WhyNow}}
        Source Summaries: {{string.Join(" | ", cleanSourceEvidence)}}

        Your output MUST be a single, valid JSON object with the following structure:
        {
          "topic": "string",
          "keyword": "string",
          "intent": "string",
          "angle": "string",
          "painPoint": "string",
          "goal": "string",
          "whyNow": "string",
          "sourceEvidence": ["string"],
          "isViable": boolean
        }
        Do not add any text before or after the JSON object.
        """;

        try
        {
            var result = await _llmClient.GenerateAsync<ResearchIntentResult>(prompt, model: "gemma4:31b-cloud", cancellationToken);
            _logger.LogInformation("ContentStrategyAgent: Generated REAL research intent via LLM for idea {IdeaId}", idea.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContentStrategyAgent: LLM call failed for idea {IdeaId}. Simulation is disabled.", idea.Id);
            throw new InvalidOperationException($"LLM provider failed and simulation is disabled: {ex.Message}");
        }
    }

    public Task<KeywordStrategyResult> BuildKeywordStrategyAsync(ContentIdea idea, IReadOnlyCollection<string> secondaryKeywords, CancellationToken cancellationToken = default)
    {
        // Clean inputs
        var cleanPrimaryKeyword = CleanTitleAndKeyword(idea.PrimaryKeyword);
        var cleanTitle = CleanTitleAndKeyword(idea.Title);
        var cleanSecondary = secondaryKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CleanTitleAndKeyword).ToList();

        var primaryKeyword = NormalizePrimaryKeyword(cleanPrimaryKeyword, cleanTitle);
        var normalizedKeyword = primaryKeyword;
        var keywordVariations = cleanSecondary
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeKeywordPhrase)
            .Where(IsNaturalKeyword)
            .Where(x => !string.Equals(x, primaryKeyword, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        foreach (var candidate in BuildFallbackKeywordVariations(normalizedKeyword))
        {
            if (IsNaturalKeyword(candidate) && !keywordVariations.Contains(candidate, StringComparer.OrdinalIgnoreCase) && !string.Equals(candidate, primaryKeyword, StringComparison.OrdinalIgnoreCase))
            {
                keywordVariations.Add(candidate);
            }
        }

        keywordVariations = keywordVariations
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var titleOptions = new[]
        {
            Safe(cleanTitle),
            $"{normalizedKeyword} for Beginners: A Simple Plan That Actually Works",
            $"How to Start {normalizedKeyword} Without Feeling Overwhelmed"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var faqQuestions = new[]
        {
            $"What is the best way to start with {normalizedKeyword}?",
            $"How does {normalizedKeyword} work in real life?",
            $"What mistakes should you avoid with {normalizedKeyword}?"
        };

        var intentMatch = string.IsNullOrWhiteSpace(idea.SearchIntent)
            ? "Intent not provided. Treat as practical beginner guide until clarified."
            : $"Match the article to {idea.SearchIntent.Trim()} intent with practical examples and direct action steps.";

        var supportingPhrases = cleanSecondary
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeKeywordPhrase)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !keywordVariations.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        return Task.FromResult(new KeywordStrategyResult(
            primaryKeyword,
            keywordVariations.ToArray(),
            supportingPhrases,
            titleOptions,
            faqQuestions,
            intentMatch,
            keywordVariations.Count >= 3));
    }

    public Task<TopicExpansionResult> BuildTopicExpansionAsync(ContentIdea idea, KeywordStrategyResult keywordStrategy, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default)
    {
        // Clean inputs
        var cleanPrimaryKeyword = CleanTitleAndKeyword(idea.PrimaryKeyword);
        var cleanTitle = CleanTitleAndKeyword(idea.Title);
        var coreIntent = string.IsNullOrWhiteSpace(idea.AudienceGoal)
            ? $"Help the reader make progress on {cleanPrimaryKeyword}."
            : Safe(idea.AudienceGoal);

        var subtopics = new List<string>
        {
            "quick answer",
            "worked example",
            "step-by-step plan",
            "practical tips",
            "common mistakes",
            "FAQ"
        };

        foreach (var phrase in keywordStrategy.SupportingPhrases.Take(6))
        {
            if (!subtopics.Contains(phrase, StringComparer.OrdinalIgnoreCase))
                subtopics.Add(phrase);
        }

        var mustAnswerQuestions = keywordStrategy.FaqQuestions.Take(5).ToArray();
        var workedExamples = new[]
        {
            "Use a realistic starter example with simple numbers.",
            "Show the first small win a reader could achieve this week."
        };

        return Task.FromResult(new TopicExpansionResult(
            coreIntent,
            subtopics.Take(10).ToArray(),
            new[] { "quick answer", "worked example", "real numbers", "step-by-step plan" },
            mustAnswerQuestions,
            workedExamples,
            true));
    }

    public Task<ContentBriefResult> BuildContentBriefAsync(ContentIdea idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default)
    {
        // Clean inputs
        var cleanTitle = CleanTitleAndKeyword(idea.Title);
        var cleanKeyword = CleanTitleAndKeyword(idea.PrimaryKeyword);
        var cleanSummary = string.IsNullOrWhiteSpace(article.Summary) ? Safe(idea.Summary) : article.Summary;
        var cleanSecondary = secondaryKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(CleanTitleAndKeyword).ToList();

        var missingFields = GetMissingBriefFields(idea, cleanSummary);

        if (missingFields.Length > 0)
        {
            var message = $"Cannot build content brief for idea {idea.Id}. Missing required topic fields: {string.Join(", ", missingFields)}.";
            _logger.LogWarning("{Message}", message);
            throw new InvalidOperationException(message);
        }

        // --- USE REFINED TITLE ---
        var refinedTitle = RefineAndSelectBestTitleAsync(idea, article, cleanSecondary, cancellationToken).Result;

        var brief = new[]
        {
            $"Target reader problem: {Safe(idea.AudiencePainPoint)}",
            $"Primary outcome: {Safe(idea.AudienceGoal)}",
            $"Recommended angle: {Safe(idea.RecommendedAngle)}",
            $"Why now: {Safe(idea.WhyNow)}",
            $"Draft summary: {cleanSummary}",
            $"Estimated length: {article.EstimatedWordCount} words"
        };

        var hasCompleteBrief = brief.All(line => !string.IsNullOrWhiteSpace(line.Split(':', 2).ElementAtOrDefault(1)));

        return Task.FromResult(new ContentBriefResult(
            refinedTitle, // <-- USE THE REFINED TITLE
            brief,
            cleanSecondary.Where(x => !string.IsNullOrWhiteSpace(x)).Take(8).ToArray(),
            sourceSummaries.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5).ToArray(),
            article.EstimatedWordCount,
            hasCompleteBrief));
    }

    public Task<ArticleOutlineResult> CreateArticleOutlineAsync(ContentIdea idea, GeneratedLongformArticle article, TopicExpansionResult topicExpansion, CancellationToken cancellationToken = default)
    {
        // Clean inputs (though outline uses structure more than text)
        var required = topicExpansion.CoverageRequirements.Distinct().ToArray();
        var sections = new List<OutlineSectionResult>();

        // Core layout priorities
        sections.Add(new OutlineSectionResult("intro", "Introduction and Hook"));
        
        if (topicExpansion.Subtopics.Any(s => s.Contains("quick", StringComparison.OrdinalIgnoreCase)))
            sections.Add(new OutlineSectionResult("quick_answer", "The Quick Answer"));

        if (topicExpansion.Subtopics.Any(s => s.Contains("example", StringComparison.OrdinalIgnoreCase)))
            sections.Add(new OutlineSectionResult("worked_example", "Worked Example with Real Numbers"));

        foreach (var subtopic in topicExpansion.Subtopics.Where(s => !IsCoreLayout(s)))
        {
            sections.Add(new OutlineSectionResult(Slugify(subtopic), subtopic));
        }

        sections.Add(new OutlineSectionResult("steps", "Step-by-Step Action Plan"));
        sections.Add(new OutlineSectionResult("mistakes", "Common Mistakes to Avoid"));
        sections.Add(new OutlineSectionResult("faq", "Frequently Asked Questions"));
        sections.Add(new OutlineSectionResult("cta", "Your Next Step"));

        return Task.FromResult(new ArticleOutlineResult(
            // Use cleaned title for consistency, though outline is more structural
            CleanTitleAndKeyword(idea.Title),
            idea.ContentType,
            article.TargetWordCountMin,
            article.TargetWordCountMax,
            article.EstimatedWordCount,
            required,
            sections.ToArray(),
            sections.Count >= 8));
    }

    // --- UNCHANGED HELPERS ---
    private static bool IsCoreLayout(string subtopic)
    {
        var s = subtopic.ToLowerInvariant();
        return s.Contains("quick") || s.Contains("example") || s.Contains("step") || s.Contains("mistake") || s.Contains("faq") || s.Contains("cta");
    }

    private static string Slugify(string text) => 
        System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');

    private static string GetSectionId(string heading, int index)
    {
        var normalized = heading.Trim().ToLowerInvariant();
        if (normalized.Contains("quick")) return "quick_answer";
        if (normalized.Contains("example")) return "worked_example";
        if (normalized.Contains("step")) return "steps";
        if (normalized.Contains("tip")) return "tips";
        if (normalized.Contains("mistake")) return "mistakes";
        if (normalized.Contains("faq")) return "faq";
        if (normalized.Contains("next") || normalized.Contains("cta")) return "cta";
        if (index == 0) return "intro";
        return $"section_{index + 1}";
    }

    private static string[] GetMissingBriefFields(ContentIdea idea, string summary)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(idea.AudiencePainPoint)) missing.Add(nameof(idea.AudiencePainPoint));
        if (string.IsNullOrWhiteSpace(idea.AudienceGoal)) missing.Add(nameof(idea.AudienceGoal));
        if (string.IsNullOrWhiteSpace(idea.RecommendedAngle)) missing.Add(nameof(idea.RecommendedAngle));
        if (string.IsNullOrWhiteSpace(idea.WhyNow)) missing.Add(nameof(idea.WhyNow));
        if (string.IsNullOrWhiteSpace(summary)) missing.Add(nameof(idea.Summary));

        return missing.ToArray();
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static List<string> BuildFallbackKeywordVariations(string normalizedKeyword)
    {
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return new List<string>();

        return new List<string>
        {
            $"{normalizedKeyword} for beginners",
            $"{normalizedKeyword} tips",
            $"{normalizedKeyword} guide",
            $"how to {normalizedKeyword}",
            $"best {normalizedKeyword} strategies"
        };
    }
}