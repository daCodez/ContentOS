using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Research;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ContentOS.Infrastructure.Ideation;

public class IdeationAgent : IIdeationAgent
{
    private readonly IWorkflowArticleWriter _writer;
    private readonly ITopicDiversityScorer _diversityScorer;
    private readonly IIdeaDeduplicator _deduplicator;
    private readonly ILogger<IdeationAgent> _logger;

    public IdeationAgent(
        IWorkflowArticleWriter writer,
        ITopicDiversityScorer diversityScorer,
        IIdeaDeduplicator deduplicator,
        ILogger<IdeationAgent> logger)
    {
        _writer = writer;
        _diversityScorer = diversityScorer;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task<IList<CandidateContentIdea>> GenerateIdeasAsync(
        ResearchContext context,
        IList<ResearchFinding> findings,
        CancellationToken cancellationToken = default)
    {
        if (findings == null || findings.Count == 0)
            return new List<CandidateContentIdea>();

        // --- EXTRACT INSIGHTS FROM FINDINGS (NOT RAW TITLES) ---
        var insights = ExtractInsights(findings);
        var sourceTitles = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.SourceTitle))
            .Select(f => f.SourceTitle.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var prompt = BuildIdeationPrompt(context, findings, insights);
        var llmResponse = await _writer.GenerateIdeationResponseAsync(prompt, cancellationToken);

        List<CandidateContentIdea> generated;

        if (llmResponse?.Ideas != null && llmResponse.Ideas.Count > 0)
        {
            generated = llmResponse.Ideas
                .Where(i => !string.IsNullOrWhiteSpace(i.Title) && !string.IsNullOrWhiteSpace(i.PrimaryKeyword))
                .Select(i =>
                {
                    var title = i.Title.Trim();
                    var keyword = i.PrimaryKeyword.Trim();
                    var painPoint = Safe(i.AudiencePainPoint, $"readers need a simpler, clearer way to make progress with {keyword}");
                    var goal = Safe(i.AudienceGoal, $"understand {keyword} and apply it with a realistic step-by-step plan");
                    var angle = Safe(i.RecommendedAngle, $"explain {keyword} in plain language with clear examples and realistic next steps");
                    var whyNow = Safe(i.WhyNow, $"Current research signals suggest readers are actively looking for help with {keyword}.");

                    return new CandidateContentIdea
                    {
                        Title = title,
                        Summary = $"A practical article for readers who are dealing with {ToLowerPhrase(painPoint)} and want to {goal.TrimEnd('.').ToLowerInvariant()}.",
                        PrimaryKeyword = keyword,
                        SecondaryKeywords = i.SecondaryKeywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
                        SearchIntent = Safe(i.SearchIntent, "Informational"),
                        IntentType = Safe(i.SearchIntent, "Informational"),
                        ContentType = DetermineContentType(i.Title, i.PrimaryKeyword, i.SearchIntent),
                        ContentBucket = DetermineContentBucket(i.SearchIntent),
                        AudiencePainPoint = painPoint,
                        AudienceGoal = goal,
                        RecommendedAngle = angle,
                        WhyNow = whyNow,
                        Evergreen = i.Evergreen,
                        Seasonal = i.Seasonal,
                        TopicType = TopicTypeLabels.FromLabel(i.TopicType) ?? TopicType.Problem,
                        TopicTypeLabel = Safe(i.TopicType, "Problem"),
                        SpecificityTag = Safe(i.SpecificityTag, "general"),
                        SupportingFindings = new List<ResearchFinding>()
                    };
                })
                .GroupBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        else
        {
            generated = GenerateFallbackIdeas(context, findings);
        }

        // --- USE CENTRALIZED DEDUPLICATION SERVICE ---
        var processedIdeas = new List<CandidateContentIdea>();

        foreach (var raw in generated)
        {
            // --- STEP 1: CLEAN TITLE AND KEYWORD (STRIP SOURCES) ---
            var cleanTitle = _deduplicator.CleanTitleAndKeyword(raw.Title);
            var cleanKeyword = _deduplicator.CleanTitleAndKeyword(raw.PrimaryKeyword);

            if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(cleanKeyword))
            {
                _logger.LogWarning("Idea rejected after cleaning: Title='{Title}', Keyword='{Keyword}'", raw.Title, raw.PrimaryKeyword);
                continue; // Skip if cleaning resulted in empty strings
            }

            // --- STEP 2: CHECK FOR DUPLICATE USING CENTRALIZED SERVICE ---
            if (_deduplicator.IsDuplicate(raw, processedIdeas))
            {
                _logger.LogWarning("Duplicate idea rejected: Title='{Title}' (same topic, angle, intent, or pain point)", raw.Title);
                continue; // Reject duplicate
            }

            // --- STEP 2b: REJECT IF TOO SIMILAR TO A SOURCE TITLE ---
            if (_deduplicator.IsTooSimilarToSource(cleanTitle, sourceTitles))
            {
                _logger.LogWarning("Idea too similar to source title, rejected: '{Title}'", cleanTitle);
                continue;
            }

            // --- STEP 3: APPLY SCORING PENALTIES ---
            decimal adjustedScore = raw.OverallScore; // Start with the agent's base score

            // Penalty for vague title (e.g., "Guide to X", "Tips for Y")
            if (Regex.IsMatch(cleanTitle, @"^\s*(.+?)\s*(guide|tips|ways|methods|steps)\s*$", RegexOptions.IgnoreCase) && cleanTitle.Split(' ').Length < 6)
            {
                adjustedScore -= 2.0m;
                _logger.LogInformation("Applied vague title penalty: {Title}", cleanTitle);
            }

            // Penalty for keyword stuffing in title
            int keywordCountInTitle = Regex.Matches(cleanTitle, Regex.Escape(cleanKeyword), RegexOptions.IgnoreCase).Count;
            if (keywordCountInTitle > 2)
            {
                adjustedScore -= 3.0m;
                _logger.LogInformation("Applied keyword stuffing penalty: {Title}", cleanTitle);
            }

            // --- STEP 4: ENFORCE KEYWORD RULES (LENGTH, PLATFORMS, BRANDS, DATES) ---
            var keywordWords = cleanKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (keywordWords.Length < 2 || keywordWords.Length > 5)
            {
                _logger.LogWarning("Keyword length rejected: '{Keyword}' (must be 2-5 words)", cleanKeyword);
                continue;
            }

            if (keywordWords.Any(w => BannedPlatforms.Contains(w.ToLowerInvariant())))
            {
                _logger.LogWarning("Keyword contains banned platform: '{Keyword}'", cleanKeyword);
                continue;
            }

            // Reject if keyword is a likely brand (single word, all caps, or title case) unless it's a known generic
            if (keywordWords.Length == 1 && (char.IsUpper(cleanKeyword[0]) || cleanKeyword.ToLowerInvariant() != cleanKeyword))
            {
                var generics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "budget", "save", "debt", "income", "expense", "bank", "credit", "loan", "invest", "retirement", "tax", "bill", "money", "cash", "fund" };
                if (!generics.Contains(cleanKeyword.ToLowerInvariant()))
                {
                    _logger.LogWarning("Keyword likely a brand and rejected: '{Keyword}'", cleanKeyword);
                    continue;
                }
            }

            // Reject if keyword contains a 4-digit year (unless niche is historical)
            if (Regex.IsMatch(cleanKeyword, @"\b\d{4}\b") && !context.Niche.Contains("history", StringComparison.OrdinalIgnoreCase) && !context.Niche.Contains("ww2", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Keyword contains year and rejected: '{Keyword}'", cleanKeyword);
                continue;
            }

            // --- STEP 5: BUILD CANDIDATE WITH CLEANED DATA AND ADJUSTED SCORE ---
            var candidate = new CandidateContentIdea
            {
                Title = cleanTitle,
                Summary = raw.Summary,
                PrimaryKeyword = cleanKeyword,
                SecondaryKeywords = raw.SecondaryKeywords,
                SearchIntent = raw.SearchIntent,
                IntentType = raw.IntentType,
                ContentType = raw.ContentType,
                ContentBucket = raw.ContentBucket,
                AudiencePainPoint = raw.AudiencePainPoint,
                AudienceGoal = raw.AudienceGoal,
                RecommendedAngle = raw.RecommendedAngle,
                WhyNow = raw.WhyNow,
                Evergreen = raw.Evergreen,
                Seasonal = raw.Seasonal,
                SupportingFindings = raw.SupportingFindings,
                TopicType = raw.TopicType,
                TopicTypeLabel = raw.TopicTypeLabel,
                SpecificityTag = raw.SpecificityTag,
                OverallScore = adjustedScore, // Use the penalized score
                IsHighCompetition = raw.IsHighCompetition,
                CompetitionModifier = raw.CompetitionModifier,
                LowCompetitionBoost = raw.LowCompetitionBoost,
                MonetizationViable = raw.MonetizationViable,
                MonetizationWarning = raw.MonetizationWarning
            };

            processedIdeas.Add(candidate);
        }

        // --- STEP 6: ENFORCE ANGLE DIVERSITY (min 2 per angle type, unique angles) ---
        var requiredAngles = new[] { "emotional", "beginner", "mistakes", "practical", "contrarian" };
        var angleBuckets = processedIdeas
            .GroupBy(c => ClassifyAngle(c.RecommendedAngle, c.Title, c.AudiencePainPoint))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.OverallScore).ToList());

        var diversifiedIdeas = new List<CandidateContentIdea>();
        var usedAngles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: take top ideas from each required angle bucket (min 2 each)
        foreach (var requiredAngle in requiredAngles)
        {
            if (!angleBuckets.TryGetValue(requiredAngle, out var bucket)) continue;

            int taken = 0;
            foreach (var candidate in bucket)
            {
                if (taken >= 2) break;

                var angleNorm = candidate.RecommendedAngle?.Trim().ToLowerInvariant() ?? "";
                if (!string.IsNullOrWhiteSpace(angleNorm) && usedAngles.Contains(angleNorm)) continue;

                usedAngles.Add(angleNorm);
                diversifiedIdeas.Add(candidate);
                taken++;
            }
        }

        // Second pass: fill remaining from any bucket (max 2 per type, unique angles)
        var typeGroups = processedIdeas
            .Where(c => !diversifiedIdeas.Contains(c))
            .GroupBy(c => DetectIdeaType(c.Title, c.RecommendedAngle, c.AudiencePainPoint))
            .ToList();

        foreach (var group in typeGroups)
        {
            var sortedGroup = group.OrderByDescending(c => c.OverallScore).ToList();
            int takenFromThisGroup = 0;
            foreach (var candidate in sortedGroup)
            {
                if (takenFromThisGroup >= 2) break;

                var angleNorm = candidate.RecommendedAngle?.Trim().ToLowerInvariant() ?? "";
                if (!string.IsNullOrWhiteSpace(angleNorm) && usedAngles.Contains(angleNorm)) continue;

                usedAngles.Add(angleNorm);
                diversifiedIdeas.Add(candidate);
                takenFromThisGroup++;
            }
        }

        // --- STEP 7: FINAL SORT AND RETURN ---
        var final = diversifiedIdeas
            .OrderByDescending(c => c.OverallScore)
            .ToList();

        _logger.LogInformation(
            "Post-processing: {Count} ideas. Types: {Breakdown}. Scores: {Scores}",
            final.Count,
            string.Join(", ", final.GroupBy(i => i.TopicTypeLabel).Select(g => $"{g.Key}={g.Count()}")),
            string.Join(", ", final.Select(i => $"{i.Title[..Math.Min(30, i.Title.Length)]}={i.OverallScore}")));

        return final;
    }

    // --- PRIVATE HELPERS ---
    private static readonly HashSet<string> BannedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "reddit", "youtube", "facebook", "twitter", "instagram", "tiktok", "pinterest", "linkedin", "quora", "medium"
    };

    private static string BuildIdeationPrompt(ResearchContext context, IList<ResearchFinding> findings, List<ResearchInsight> insights)
    {
        var lines = new List<string>
        {
            $"Site: {context.SiteName}",
            $"Niche: {context.Niche}",
            $"Audience: {context.AudienceDescription}",
            "",
            "Generate ORIGINAL content ideas derived from reader pain points, NOT from source titles.",
            "",
            "CRITICAL RULES:",
            "1. Do NOT copy, rephrase, or mirror any source title. Ideas must be ORIGINAL transformations.",
            "2. Do NOT include source names, publishers, or platform references in any title or keyword.",
            "3. Do NOT use list-pattern titles like '10 tips', 'complete guide', 'ultimate guide' unless genuinely justified.",
            "4. Every idea MUST have a topicType: Problem, Solution, Comparison, Outcome, Scenario, Authority, CTR",
            "5. Every idea MUST have a specificityTag: dollar-amount, audience-specific, tool-based, comparison, outcome-driven, number-specific, or general",
            "6. Every idea MUST have a recommendedAngle from this list: emotional, beginner, mistakes, practical, contrarian, comparison, myth-busting, scenario, authority, quick-win",
            "7. You MUST produce at least 2 ideas for EACH of these angle types: emotional, beginner, mistakes, practical, contrarian",
            "8. Every title must derive from a pain point or frustration, not from a source headline.",
            "9. Specificity is MANDATORY: real dollar amounts, specific audience names, tool names, or timeframes.",
            "10. Max 2 Problem-type topics. Prefer LOW-COMPETITION angles.",
            "11. Every topic MUST have a natural monetization path.",
            "",
            "Angle type definitions:",
            "  emotional  => feelings, fears, frustrations, shame, anxiety, relief",
            "  beginner   => first-time, overwhelmed, don't know where to start, 101-level",
            "  mistakes   => things people get wrong, why they fail, what to avoid",
            "  practical  => step-by-step, how-to, actionable systems, tools",
            "  contrarian => challenge common wisdom, myth-busting, unpopular takes, 'what nobody tells you'",
            "",
            "Return JSON: { \"ideas\": [ { \"title\": string, \"primaryKeyword\": string, \"secondaryKeywords\": string[], \"searchIntent\": string, \"audiencePainPoint\": string, \"audienceGoal\": string, \"recommendedAngle\": string, \"whyNow\": string, \"evergreen\": boolean, \"seasonal\": boolean, \"topicType\": string, \"specificityTag\": string } ] }",
            "",
            "RESEARCH INSIGHTS (derive ideas from these, NOT from titles):"
        };

        foreach (var insight in insights)
        {
            if (!string.IsNullOrWhiteSpace(insight.PainPoint))
                lines.Add($"- Pain Point: {insight.PainPoint}");
            if (!string.IsNullOrWhiteSpace(insight.Pattern))
                lines.Add($"- Pattern: {insight.Pattern}");
            if (!string.IsNullOrWhiteSpace(insight.Gap))
                lines.Add($"- Gap: {insight.Gap}");
            if (!string.IsNullOrWhiteSpace(insight.EmotionalTrigger))
                lines.Add($"- Emotion: {insight.EmotionalTrigger}");
            if (insight.Keywords.Count > 0)
                lines.Add($"- Keywords: {string.Join(", ", insight.Keywords.Take(3))}");
        }
        return string.Join("\n", lines);
    }


    private static List<ResearchInsight> ExtractInsights(IList<ResearchFinding> findings)
    {
        var insights = new List<ResearchInsight>();

        // Group by pain points
        var painGroups = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.PainPoint))
            .GroupBy(f => f.PainPoint.Trim().ToLowerInvariant())
            .Where(g => g.Count() >= 1)
            .ToList();

        foreach (var group in painGroups.Take(10))
        {
            var titles = group.Select(f => f.SourceTitle.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            var keywords = group
                .Where(f => !string.IsNullOrWhiteSpace(f.KeywordSuggestion))
                .Select(f => f.KeywordSuggestion.Trim().ToLowerInvariant())
                .Distinct()
                .Take(5)
                .ToList();

            insights.Add(new ResearchInsight
            {
                PainPoint = group.Key,
                Pattern = ExtractPatternFromTitles(titles),
                Gap = ExtractGapFromFindings(group.ToList()),
                EmotionalTrigger = ExtractEmotionalTrigger(group.ToList()),
                SourceTitles = titles,
                Keywords = keywords
            });
        }

        // Also extract from findings without explicit pain points
        var remaining = findings
            .Where(f => string.IsNullOrWhiteSpace(f.PainPoint))
            .Take(8)
            .ToList();

        foreach (var finding in remaining)
        {
            var titles = new List<string> { finding.SourceTitle?.Trim() ?? string.Empty };
            var keywords = new List<string>();
            if (!string.IsNullOrWhiteSpace(finding.KeywordSuggestion))
                keywords.Add(finding.KeywordSuggestion.Trim().ToLowerInvariant());

            insights.Add(new ResearchInsight
            {
                PainPoint = !string.IsNullOrWhiteSpace(finding.ObservedPhrase) ? finding.ObservedPhrase.Trim() : $"readers searching for {finding.KeywordSuggestion?.Trim() ?? finding.TopicSuggestion?.Trim() ?? "information"}",
                Pattern = ExtractPatternFromTitles(titles),
                Gap = !string.IsNullOrWhiteSpace(finding.Notes) ? finding.Notes.Trim() : string.Empty,
                EmotionalTrigger = string.Empty,
                SourceTitles = titles.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(),
                Keywords = keywords
            });
        }

        return insights;
    }

    private static string ExtractPatternFromTitles(List<string> titles)
    {
        if (titles == null || titles.Count == 0) return string.Empty;

        // Detect common patterns in source titles
        var combined = string.Join(" ", titles).ToLowerInvariant();

        if (combined.Contains("mistake") || combined.Contains("wrong") || combined.Contains("fail") || combined.Contains("avoid"))
            return "Most advice focuses on what to do, but readers struggle more with what NOT to do and why they keep making the same errors.";
        if (combined.Contains("beginner") || combined.Contains("start") || combined.Contains("first") || combined.Contains("101"))
            return "Beginners are overwhelmed by too many options and need a simple starting point, not comprehensive guides.";
        if (combined.Contains("budget") || combined.Contains("save") || combined.Contains("cheap") || combined.Contains("free"))
            return "Readers with limited budgets need realistic strategies, not generic advice that assumes disposable income.";
        if (combined.Contains("vs") || combined.Contains("compare") || combined.Contains("better") || combined.Contains("which"))
            return "Readers face choice paralysis and need honest comparisons, not sponsored reviews.";

        return "Sources tend to cover the same surface-level advice without addressing the underlying frustrations that keep readers stuck.";
    }

    private static string ExtractGapFromFindings(List<ResearchFinding> findings)
    {
        if (findings == null || findings.Count == 0) return string.Empty;

        var notes = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.Notes))
            .Select(f => f.Notes.Trim())
            .ToList();

        if (notes.Count == 0)
            return "No explicit gap identified — the opportunity is in approaching the topic from a different angle than existing content.";

        return string.Join("; ", notes.Take(2));
    }

    private static string ExtractEmotionalTrigger(List<ResearchFinding> findings)
    {
        if (findings == null || findings.Count == 0) return string.Empty;

        var painPoints = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.PainPoint))
            .Select(f => f.PainPoint.Trim().ToLowerInvariant())
            .ToList();

        var combined = string.Join(" ", painPoints);

        if (combined.Contains("overwhelm") || combined.Contains("stress") || combined.Contains("anxious") || combined.Contains("confus"))
            return "Readers feel overwhelmed and confused — they need clarity and a simple path forward, not more information.";
        if (combined.Contains("stuck") || combined.Contains("fail") || combined.Contains("give up") || combined.Contains("quit"))
            return "Readers feel stuck in a cycle of trying and failing — they need a realistic approach that accounts for their actual situation.";
        if (combined.Contains("scared") || combined.Contains("afraid") || combined.Contains("worry") || combined.Contains("fear"))
            return "Fear and anxiety are driving search behavior — content should acknowledge these feelings before offering solutions.";
        if (combined.Contains("frustrat") || combined.Contains("annoy") || combined.Contains("tired") || combined.Contains("sick of"))
            return "Frustration is high — readers are tired of generic advice and want something that actually works for their specific situation.";

        return string.Empty;
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string DetermineContentType(string title, string keyword, string searchIntent)
    {
        var titleLower = title.ToLowerInvariant();
        var keywordLower = keyword.ToLowerInvariant();
        var intentLower = searchIntent?.ToLowerInvariant() ?? "informational";

        if (titleLower.Contains("vs ") || titleLower.Contains(" versus ") || titleLower.Contains(" compared to ") ||
            titleLower.Contains("better than") || titleLower.Contains("alternative to"))
            return "Comparison";

        if (titleLower.StartsWith("how to") || titleLower.StartsWith("ways to") || titleLower.StartsWith("steps to") ||
            intentLower == "how-to" || keywordLower.Contains(" how to ") || keywordLower.Contains(" tutorial"))
            return "HowTo";

        if (titleLower.Contains("mistake") || titleLower.Contains("error") || titleLower.Contains("fail") ||
            titleLower.Contains("wrong") || titleLower.Contains("avoid") || titleLower.Contains("pitfall"))
            return "Mistakes";

        if (titleLower.Contains("myth") || titleLower.Contains("fact vs") || titleLower.Contains("debunk") ||
            titleLower.Contains("truth about") || titleLower.Contains("secret") || titleLower.Contains("nobody tells you"))
            return "Myth";

        if (titleLower.Contains("case study") || titleLower.Contains("example") || titleLower.Contains("real life") ||
            titleLower.Contains("example") || titleLower.Contains("story") || titleLower.Contains("journey"))
            return "CaseStudy";

        if (titleLower.Contains("checklist") || titleLower.Contains("template") || titleLower.Contains("worksheet") ||
            titleLower.Contains("planner") || titleLower.Contains("calendar") || titleLower.Contains("tracker"))
            return "Checklist";

        if (titleLower.Contains("list") || titleLower.Contains("top ") || titleLower.Contains("best ") ||
            titleLower.Contains("resources") || titleLower.Contains("tools") || titleLower.Contains("apps") ||
            titleLower.Contains("websites") || titleLower.Contains("books"))
            return "List";

        if (intentLower == "transactional" || titleLower.Contains("review") || titleLower.Contains("buy") ||
            titleLower.Contains("price") || titleLower.Contains("cost") || titleLower.Contains("purchase"))
            return "Review";

        return "Article";
    }

    private static string DetermineContentBucket(string searchIntent)
    {
        return searchIntent?.ToLowerInvariant() switch
        {
            "informational" => "Awareness",
            "navigational" => "Awareness",
            "commercial" => "Consideration",
            "transactional" => "Decision",
            _ => "Awareness"
        };
    }

    private static string ClassifyAngle(string recommendedAngle, string title, string painPoint)
    {
        var combined = $"{recommendedAngle} {title} {painPoint}".ToLowerInvariant();

        if (combined.Contains("emotion") || combined.Contains("fear") || combined.Contains("frustrat") ||
            combined.Contains("shame") || combined.Contains("anxiety") || combined.Contains("overwhelm") ||
            combined.Contains("feel") || combined.Contains("worry") || combined.Contains("stress"))
            return "emotional";

        if (combined.Contains("beginner") || combined.Contains("first") || combined.Contains("start") ||
            combined.Contains("101") || combined.Contains("basics") || combined.Contains("getting started") ||
            combined.Contains("new to"))
            return "beginner";

        if (combined.Contains("mistake") || combined.Contains("wrong") || combined.Contains("fail") ||
            combined.Contains("error") || combined.Contains("avoid") || combined.Contains("pitfall") ||
            combined.Contains("don't") || combined.Contains("never"))
            return "mistakes";

        if (combined.Contains("step-by-step") || combined.Contains("how to") || combined.Contains("practical") ||
            combined.Contains("actionable") || combined.Contains("tool") || combined.Contains("system") ||
            combined.Contains("template") || combined.Contains("method"))
            return "practical";

        if (combined.Contains("contrarian") || combined.Contains("myth") || combined.Contains("debunk") ||
            combined.Contains("truth about") || combined.Contains("nobody tells") || combined.Contains("secret") ||
            combined.Contains("unpopular") || combined.Contains("wrong about"))
            return "contrarian";

        // Default based on title signals
        if (combined.Contains("vs") || combined.Contains("compare") || combined.Contains("better"))
            return "practical";

        return "practical"; // default bucket
    }

    private static string DetectIdeaType(string title, string angle, string painPoint)
    {
        var combined = $"{title} {angle} {painPoint}".ToLowerInvariant();

        if (combined.Contains("how to") || combined.Contains("ways to") || combined.Contains("steps to") ||
            combined.Contains("tutorial") || combined.Contains("guide") || combined.Contains("step by step"))
            return "HowTo";

        if (combined.Contains("mistake") || combined.Contains("error") || combined.Contains("fail") ||
            combined.Contains("wrong") || combined.Contains("avoid") || combined.Contains("pitfall") ||
            combined.Contains("don't") || combined.Contains("never"))
            return "Mistakes";

        if (combined.Contains("myth") || combined.Contains("fact vs") || combined.Contains("debunk") ||
            combined.Contains("truth about") || combined.Contains("secret") || combined.Contains("nobody tells you"))
            return "Myth";

        if (combined.Contains("case study") || combined.Contains("example") || combined.Contains("real life") ||
            combined.Contains("story") || combined.Contains("journey") || combined.Contains("experience"))
            return "CaseStudy";

        if (combined.Contains("checklist") || combined.Contains("template") || combined.Contains("worksheet") ||
            combined.Contains("planner") || combined.Contains("calendar") || combined.Contains("tracker"))
            return "Checklist";

        if (combined.Contains("list") || combined.Contains("top ") || combined.Contains("best ") ||
            combined.Contains("resources") || combined.Contains("tools") || combined.Contains("apps") ||
            combined.Contains("websites") || combined.Contains("books"))
            return "List";

        if (combined.Contains("review") || combined.Contains("compare") || combined.Contains("vs") ||
            combined.Contains("versus") || combined.Contains("better than") || combined.Contains("alternative to"))
            return "Comparison";

        if (combined.Contains("beginner") || combined.Contains("start") || combined.Contains("getting started") ||
            combined.Contains("introduction") || combined.Contains("basics") || combined.Contains("101"))
            return "Beginner";

        return "General";
    }

    private static string ToLowerPhrase(string value)
    {
        var cleaned = value.Trim().TrimEnd('.');
        return cleaned.Length == 0 ? cleaned : char.ToLowerInvariant(cleaned[0]) + cleaned[1..];
    }

    private static List<CandidateContentIdea> GenerateFallbackIdeas(ResearchContext context, IList<ResearchFinding> findings)
    {
        var ideas = new List<CandidateContentIdea>();

        // Generate a few basic ideas based on findings
        foreach (var finding in findings.Take(3))
        {
            var keyword = !string.IsNullOrWhiteSpace(finding.KeywordSuggestion) ? finding.KeywordSuggestion.Trim() : context.Niche.Trim();
            if (string.IsNullOrWhiteSpace(keyword)) keyword = "topic";

            ideas.Add(new CandidateContentIdea
            {
                Title = $"Understanding {keyword}: A Complete Guide",
                Summary = $"A comprehensive guide to understanding and working with {keyword}.",
                PrimaryKeyword = keyword,
                SecondaryKeywords = new List<string> { keyword + " guide", keyword + " tutorial", keyword + " tips" },
                SearchIntent = "Informational",
                IntentType = "Informational",
                ContentType = "HowTo",
                ContentBucket = "Awareness",
                AudiencePainPoint = $"Struggling to understand {keyword}",
                AudienceGoal = $"Learn {keyword} with clear, practical examples",
                RecommendedAngle = "beginner-friendly",
                WhyNow = $"Increasing interest in {keyword} as readers seek practical solutions",
                Evergreen = true,
                Seasonal = false,
                SupportingFindings = new List<ResearchFinding> { finding },
                TopicType = TopicType.Problem,
                TopicTypeLabel = "Problem",
                SpecificityTag = "beginner",
                OverallScore = 5.0m
            });
        }

        return ideas;
    }
}