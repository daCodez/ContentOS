using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Research.Serp;

namespace ContentOS.Infrastructure.Scoring;

public sealed class DefaultOptimizationScoringService : IOptimizationScoringService
{
    public OptimizationScoreReport Compute(ContentIdea idea, GeneratedLongformArticle article, SerpBenchmarkReport benchmark)
    {
        var primaryKeyword = idea.PrimaryKeyword?.Trim() ?? string.Empty;
        var keywordBenchmarks = BuildKeywordBenchmarks(idea, benchmark);
        var relatedKeywords = keywordBenchmarks.Select(x => x.Keyword).ToList();
        var normalizedText = article.FullText ?? string.Empty;
        var actualWordCount = CountWords(normalizedText);
        var actualPrimaryKeywordCount = CountPhraseOccurrences(normalizedText, primaryKeyword);
        var primaryKeywordDensity = actualWordCount == 0 ? 0m : Math.Round((decimal)actualPrimaryKeywordCount / actualWordCount * 100m, 2);
        var actualLinksCount = CountLinks(normalizedText);
        var actualMediaCount = CountMedia(normalizedText);
        var completedChecklistItems = CountCompletedChecklist(article, primaryKeyword);
        var relatedKeywordChips = keywordBenchmarks
            .Select(benchmarkKeyword => new KeywordChip
            {
                Keyword = benchmarkKeyword.Keyword,
                ActualCount = CountPhraseOccurrences(normalizedText, benchmarkKeyword.Keyword),
                TargetMin = benchmarkKeyword.RecommendedMin,
                TargetMax = benchmarkKeyword.RecommendedMax,
                Score = ScoreRange(
                    CountPhraseOccurrences(normalizedText, benchmarkKeyword.Keyword),
                    benchmarkKeyword.RecommendedMin,
                    benchmarkKeyword.RecommendedMax)
            })
            .ToList();

        var relatedKeywordsFoundCount = relatedKeywordChips.Count(x => x.ActualCount > 0);
        var synonymCoverageScore = ScoreRange(relatedKeywordsFoundCount, 5, Math.Min(10, Math.Max(5, relatedKeywordChips.Count)));
        var missingRelatedKeywords = relatedKeywordChips.Where(x => x.ActualCount <= 0).Select(x => x.Keyword).Take(8).ToList();
        var overusedKeywords = relatedKeywordChips.Where(x => x.ActualCount > x.TargetMax).Select(x => x.Keyword).Take(8).ToList();

        var primaryKeywordScore = ScoreRange(actualPrimaryKeywordCount, benchmark.RecommendedPrimaryKeywordMin, benchmark.RecommendedPrimaryKeywordMax);
        var placementQualityScore = ScorePlacement(article, primaryKeyword);
        var exactMatchOveruseRisk = ScoreExactMatchOveruseRisk(actualPrimaryKeywordCount, benchmark.RecommendedPrimaryKeywordMin, benchmark.RecommendedPrimaryKeywordMax);
        var densityRisk = ScoreDensityRisk(primaryKeywordDensity);
        var headingOveruseRisk = ScoreHeadingOveruseRisk(article, primaryKeyword);
        var anchorOveruseRisk = ScoreAnchorOveruseRisk(normalizedText, primaryKeyword);
        var naturalnessPenalty = ScoreNaturalnessPenalty(article, primaryKeyword, primaryKeywordDensity);
        var stuffingRiskScore = Clamp((int)Math.Round(
            (exactMatchOveruseRisk * 0.40m) +
            (densityRisk * 0.20m) +
            (headingOveruseRisk * 0.15m) +
            (anchorOveruseRisk * 0.10m) +
            (naturalnessPenalty * 0.15m)));
        var naturalnessScore = Math.Max(0, 100 - stuffingRiskScore);

        var keywordOptimizationScore = Clamp((int)Math.Round(
            (primaryKeywordScore * 0.35m) +
            (synonymCoverageScore * 0.25m) +
            (placementQualityScore * 0.10m) +
            (naturalnessScore * 0.30m)));

        var contentLengthScore = ScoreRange(actualWordCount, benchmark.RecommendedWordCountMin, benchmark.RecommendedWordCountMax);
        var contentStructureScore = ScoreContentStructure(article, primaryKeyword);
        var topicCoverageScore = ScoreTopicCoverage(article, relatedKeywordsFoundCount);
        var contentQualityScore = Clamp((int)Math.Round(
            (contentStructureScore * 0.25m) +
            (contentLengthScore * 0.15m) +
            (ScoreHelpfulness(article) * 0.20m) +
            (ScoreReadability(article) * 0.15m) +
            (topicCoverageScore * 0.25m)));

        var intentMatchScore = ScoreIntentMatch(idea, article, primaryKeyword);
        var alignmentScore = ScoreTopicAlignment(article, primaryKeyword);
        var headlineCtrScore = ScoreHeadline(article.Title, primaryKeyword);
        var monetizationReadinessScore = ScoreMonetization(article);
        var topicDepthScore = ScoreTopicDepth(article, relatedKeywordsFoundCount, relatedKeywordChips.Count, benchmark);

        var internalLinkingScore = ScoreRange(actualLinksCount, 2, 5);
        var mediaScore = ScoreRange(actualMediaCount, 1, 3);
        var uxReadabilityScore = ScoreReadability(article);
        var schemaTechnicalSeoScore = ScoreSchemaTechnicalSeo(article);
        var freshnessScore = ScoreFreshness(idea, article);
        var checklistScore = ScoreRange(completedChecklistItems, 8, 10);
        var linksScore = internalLinkingScore;

        var highPriorityScore = Clamp((int)Math.Round(
            (intentMatchScore * 0.15m) +
            (alignmentScore * 0.20m) +
            (contentQualityScore * 0.20m) +
            (keywordOptimizationScore * 0.15m) +
            (headlineCtrScore * 0.10m) +
            (monetizationReadinessScore * 0.10m) +
            (topicDepthScore * 0.10m)));

        var lowPriorityScore = Clamp((int)Math.Round(
            (internalLinkingScore * 0.25m) +
            (mediaScore * 0.20m) +
            (uxReadabilityScore * 0.20m) +
            (schemaTechnicalSeoScore * 0.20m) +
            (freshnessScore * 0.15m)));

        var stuffingPenaltyApplied = stuffingRiskScore >= 45 ? 15 : stuffingRiskScore >= 35 ? 8 : 0;
        var optimizationScore = Clamp((int)Math.Round(
            (highPriorityScore * 0.80m) +
            (lowPriorityScore * 0.20m) -
            stuffingPenaltyApplied));

        return new OptimizationScoreReport
        {
            OptimizationScore = optimizationScore,
            HighPriorityScore = highPriorityScore,
            LowPriorityScore = lowPriorityScore,
            StuffingRiskScore = stuffingRiskScore,
            StuffingPenaltyApplied = stuffingPenaltyApplied,
            PublishReady = optimizationScore >= 75 && highPriorityScore >= 70 && stuffingRiskScore < 45 && alignmentScore >= 60,
            IntentMatchScore = intentMatchScore,
            ContentQualityScore = contentQualityScore,
            KeywordOptimizationScore = keywordOptimizationScore,
            HeadlineCtrScore = headlineCtrScore,
            MonetizationReadinessScore = monetizationReadinessScore,
            TopicDepthScore = topicDepthScore,
            InternalLinkingScore = internalLinkingScore,
            MediaScore = mediaScore,
            UxReadabilityScore = uxReadabilityScore,
            SchemaTechnicalSeoScore = schemaTechnicalSeoScore,
            FreshnessScore = freshnessScore,
            PrimaryKeywordScore = primaryKeywordScore,
            SynonymCoverageScore = synonymCoverageScore,
            ContentStructureScore = contentStructureScore,
            ContentLengthScore = contentLengthScore,
            LinksScore = linksScore,
            ChecklistScore = checklistScore,
            PlacementQualityScore = placementQualityScore,
            NaturalnessScore = naturalnessScore,
            ActualWordCount = actualWordCount,
            TargetWordCountMin = benchmark.RecommendedWordCountMin,
            TargetWordCountMax = benchmark.RecommendedWordCountMax,
            ActualPrimaryKeywordCount = actualPrimaryKeywordCount,
            TargetPrimaryKeywordMin = benchmark.RecommendedPrimaryKeywordMin,
            TargetPrimaryKeywordMax = benchmark.RecommendedPrimaryKeywordMax,
            PrimaryKeywordDensity = primaryKeywordDensity,
            ActualLinksCount = actualLinksCount,
            TargetLinksMin = 2,
            TargetLinksMax = 5,
            ActualMediaCount = actualMediaCount,
            TargetMediaMin = 1,
            TargetMediaMax = 3,
            CompletedChecklistItems = completedChecklistItems,
            TargetChecklistMin = 8,
            TargetChecklistMax = 10,
            SynonymCoverageFoundCount = relatedKeywordsFoundCount,
            TargetSynonymCoverageCount = Math.Min(10, Math.Max(5, relatedKeywordChips.Count)),
            MissingRelatedKeywords = missingRelatedKeywords,
            OverusedKeywords = overusedKeywords,
            PrimaryKeywordChips =
            [
                new KeywordChip
                {
                    Keyword = primaryKeyword,
                    ActualCount = actualPrimaryKeywordCount,
                    TargetMin = benchmark.RecommendedPrimaryKeywordMin,
                    TargetMax = benchmark.RecommendedPrimaryKeywordMax,
                    Score = primaryKeywordScore
                }
            ],
            RelatedKeywordChips = relatedKeywordChips
        };
    }

    private static List<SerpKeywordBenchmark> BuildKeywordBenchmarks(ContentIdea idea, SerpBenchmarkReport benchmark)
    {
        var desired = ParseJsonArray(idea.SecondaryKeywordsJson)
            .Concat(BuildFallbackKeywordVariations(idea.PrimaryKeyword, idea.Title))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var known = benchmark.RelatedKeywords
            .Where(x => !string.IsNullOrWhiteSpace(x.Keyword))
            .ToDictionary(x => x.Keyword, StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in desired)
        {
            if (!known.ContainsKey(keyword))
            {
                known[keyword] = new SerpKeywordBenchmark
                {
                    Keyword = keyword,
                    RecommendedMin = 1,
                    RecommendedMax = 2,
                    AverageCount = 1
                };
            }
        }

        return known.Values.Take(12).ToList();
    }

    private static IEnumerable<string> BuildFallbackKeywordVariations(string? primaryKeyword, string? title)
    {
        var seed = string.IsNullOrWhiteSpace(primaryKeyword) ? title : primaryKeyword;
        if (string.IsNullOrWhiteSpace(seed))
        {
            yield break;
        }

        var normalized = seed.Trim();
        var head = ExtractHeadKeyword(normalized);
        var mid = BuildMidTailKeyword(head, normalized);

        yield return normalized;

        if (!string.Equals(head, normalized, StringComparison.OrdinalIgnoreCase))
        {
            yield return head;
        }

        if (!string.IsNullOrWhiteSpace(mid) &&
            !string.Equals(mid, normalized, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mid, head, StringComparison.OrdinalIgnoreCase))
        {
            yield return mid;
        }

        if (!string.IsNullOrWhiteSpace(head))
        {
            yield return $"{head} tips";
        }

        if (!normalized.Contains("for beginners", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{normalized} for beginners";
        }

        if (!normalized.StartsWith("how to ", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"how to start {normalized}";
        }
    }

    private static string ExtractHeadKeyword(string keyword)
    {
        var normalized = keyword.Trim();
        var withoutHowTo = normalized.StartsWith("how to ", StringComparison.OrdinalIgnoreCase)
            ? normalized[7..].Trim()
            : normalized;

        var splitters = new[] { " for ", " with ", " without ", " on ", " in " };
        foreach (var splitter in splitters)
        {
            var index = withoutHowTo.IndexOf(splitter, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                return withoutHowTo[..index].Trim();
            }
        }

        var parts = withoutHowTo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? withoutHowTo : string.Join(' ', parts.Take(2));
    }

    private static string BuildMidTailKeyword(string head, string keyword)
    {
        if (keyword.Contains("low income", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(head))
        {
            return $"low income {head}";
        }

        if (keyword.Contains("beginner", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(head))
        {
            return $"beginner {head}";
        }

        return string.IsNullOrWhiteSpace(head) ? keyword : $"{head} guide";
    }

    private static int ScoreIntentMatch(ContentIdea idea, GeneratedLongformArticle article, string primaryKeyword)
    {
        var score = 35;
        if (!string.IsNullOrWhiteSpace(primaryKeyword) && article.Title.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase)) score += 15;
        if (!string.IsNullOrWhiteSpace(idea.SearchIntent) && article.FullText.Contains(idea.SearchIntent, StringComparison.OrdinalIgnoreCase)) score += 10;
        if (HasSection(article, "step-by-step") || HasSection(article, "plan")) score += 15;
        if (HasSection(article, "faq")) score += 10;
        if (HasSection(article, "tool")) score += 5;
        if (article.IntroParagraphs.Count >= 3) score += 10;
        return Clamp(score);
    }

    private static int ScoreContentStructure(GeneratedLongformArticle article, string primaryKeyword)
    {
        var checks = 0;
        var total = 10;
        if (!string.IsNullOrWhiteSpace(article.Title)) checks++;
        if (!string.IsNullOrWhiteSpace(primaryKeyword) && article.Title.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase)) checks++;
        if (article.IntroParagraphs.Count >= 3) checks++;
        if (HasSection(article, "problem") || HasSection(article, "struggle")) checks++;
        if (HasSection(article, "baseline") || HasSection(article, "data")) checks++;
        if (HasSection(article, "framework")) checks++;
        if (HasSection(article, "step-by-step") || HasSection(article, "plan")) checks++;
        if (HasSection(article, "tip")) checks++;
        if (HasSection(article, "tool")) checks++;
        if (HasSection(article, "faq") && !string.IsNullOrWhiteSpace(article.CallToAction)) checks++;
        return (int)Math.Round((decimal)checks / total * 100m);
    }

    private static int ScoreHelpfulness(GeneratedLongformArticle article)
    {
        var bullets = CountBullets(article.FullText);
        var score = 30;
        if (article.Sections.Count >= 8) score += 25;
        if (bullets >= 3) score += 20;
        if (article.EstimatedWordCount >= 1800) score += 15;
        if (!string.IsNullOrWhiteSpace(article.CallToAction)) score += 10;
        return Clamp(score);
    }

    private static int ScoreReadability(GeneratedLongformArticle article)
    {
        var paragraphs = article.IntroParagraphs
            .Concat(article.Sections.SelectMany(x => x.Paragraphs))
            .Concat(article.ConclusionParagraphs)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (paragraphs.Count == 0)
        {
            return 0;
        }

        var averageWords = paragraphs.Average(x => CountWords(x));
        var shortParagraphRatio = paragraphs.Count(x => CountWords(x) <= 22) / (decimal)paragraphs.Count;
        var bulletBonus = CountBullets(article.FullText) >= 3 ? 10 : 0;
        var score = averageWords switch
        {
            <= 16 => 95,
            <= 22 => 85,
            <= 28 => 72,
            <= 35 => 58,
            _ => 40
        };

        score += (int)Math.Round(shortParagraphRatio * 10m) + bulletBonus;
        return Clamp(score);
    }

    private static int ScoreHeadline(string title, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var words = CountWords(title);
        var score = 40;
        if (!string.IsNullOrWhiteSpace(primaryKeyword) && title.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase)) score += 20;
        if (words is >= 6 and <= 14) score += 20;
        if (title.Contains(':') || title.Contains("How to", StringComparison.OrdinalIgnoreCase) || title.Contains("Best", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (title.Any(char.IsDigit)) score += 10;
        return Clamp(score);
    }

    private static int ScoreMonetization(GeneratedLongformArticle article)
    {
        var score = 20;
        if (HasSection(article, "tool")) score += 30;
        if (!string.IsNullOrWhiteSpace(article.CallToAction)) score += 25;
        if (article.FullText.Contains("download", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (article.FullText.Contains("template", StringComparison.OrdinalIgnoreCase) || article.FullText.Contains("checklist", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (article.FullText.Contains("recommend", StringComparison.OrdinalIgnoreCase)) score += 5;
        return Clamp(score);
    }

    private static int ScoreTopicDepth(GeneratedLongformArticle article, int foundRelatedKeywords, int targetRelatedKeywords, SerpBenchmarkReport benchmark)
    {
        var relatedCoverage = targetRelatedKeywords == 0 ? 0 : (int)Math.Round((decimal)foundRelatedKeywords / targetRelatedKeywords * 100m);
        var sectionDepth = article.Sections.Count >= 8 ? 90 : article.Sections.Count >= 6 ? 70 : 45;
        var lengthDepth = ScoreRange(article.EstimatedWordCount, benchmark.RecommendedWordCountMin, benchmark.RecommendedWordCountMax);
        return Clamp((int)Math.Round((relatedCoverage * 0.35m) + (sectionDepth * 0.35m) + (lengthDepth * 0.30m)));
    }

    private static int ScoreSchemaTechnicalSeo(GeneratedLongformArticle article)
    {
        var score = 20;
        if (!string.IsNullOrWhiteSpace(article.MetaDescription)) score += 25;
        if (!string.IsNullOrWhiteSpace(article.Slug)) score += 20;
        if (HasSection(article, "faq")) score += 20;
        if (!string.IsNullOrWhiteSpace(article.Title)) score += 15;
        return Clamp(score);
    }

    private static int ScoreFreshness(ContentIdea idea, GeneratedLongformArticle article)
    {
        var score = 40;
        if (!string.IsNullOrWhiteSpace(idea.WhyNow)) score += 25;
        if (article.FullText.Contains("now", StringComparison.OrdinalIgnoreCase) || article.FullText.Contains("today", StringComparison.OrdinalIgnoreCase)) score += 15;
        if (article.FullText.Contains(DateTime.UtcNow.Year.ToString(), StringComparison.OrdinalIgnoreCase)) score += 20;
        return Clamp(score);
    }

    private static int ScorePlacement(GeneratedLongformArticle article, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return 0;
        }

        var intro = string.Join(" ", article.IntroParagraphs.Take(3));
        var body = string.Join(" ", article.Sections.SelectMany(x => x.Paragraphs));
        var titleHasPrimary = article.Title.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase);
        var introCount = CountPhraseOccurrences(FirstWords(intro, 120), primaryKeyword);
        var bodyCount = CountPhraseOccurrences(body, primaryKeyword);
        var headingCount = article.Sections.Count(x => x.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        var score = 0;

        if (titleHasPrimary) score += 35;
        if (introCount == 1) score += 25;
        else if (introCount > 1) score += 5;
        if (bodyCount is >= 1 and <= 2) score += 25;
        else if (bodyCount == 0) score += 10;
        if (article.Slug.Contains(Slugify(primaryKeyword), StringComparison.OrdinalIgnoreCase)) score += 10;
        if (headingCount == 0) score += 5;

        return Clamp(score);
    }

    private static int ScoreExactMatchOveruseRisk(int actual, int min, int max)
    {
        if (actual <= max)
        {
            return 0;
        }

        var slightOver = max + Math.Max(1, (max - min) / 2 + 1);
        if (actual <= slightOver)
        {
            return 25;
        }

        var moderateOver = max + Math.Max(2, max);
        if (actual <= moderateOver)
        {
            return 60;
        }

        return 100;
    }

    private static int ScoreDensityRisk(decimal density)
        => density switch
        {
            <= 0.8m => 0,
            <= 1.2m => 10,
            <= 1.6m => 40,
            _ => 100
        };

    private static int ScoreHeadingOveruseRisk(GeneratedLongformArticle article, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return 0;
        }

        var count = article.Sections.Count(x => x.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        return count switch
        {
            0 => 0,
            1 => 20,
            2 => 60,
            _ => 100
        };
    }

    private static int ScoreAnchorOveruseRisk(string content, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return 0;
        }

        var linkPhraseCount = Regex.Matches(content, $@"\[[^\]]*{Regex.Escape(primaryKeyword)}[^\]]*\]\([^\)]*\)", RegexOptions.IgnoreCase).Count;
        return linkPhraseCount switch
        {
            <= 1 => 0,
            <= 3 => 40,
            _ => 100
        };
    }

    private static int ScoreNaturalnessPenalty(GeneratedLongformArticle article, string primaryKeyword, decimal density)
    {
        var score = 0;
        if (density > 1.2m) score += 30;
        if (HasRepeatedNearby(article.FullText, primaryKeyword)) score += 35;
        if (article.IntroParagraphs.Any(x => CountPhraseOccurrences(x, primaryKeyword) >= 2)) score += 20;
        if (article.Sections.Count(x => x.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase)) >= 2) score += 20;
        return Clamp(score);
    }

    private static bool HasRepeatedNearby(string content, string phrase)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var sentences = Regex.Split(content, @"(?<=[.!?])\s+");
        for (var index = 0; index < sentences.Length - 1; index++)
        {
            if (CountPhraseOccurrences(sentences[index], phrase) > 0 && CountPhraseOccurrences(sentences[index + 1], phrase) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int ScoreTopicCoverage(GeneratedLongformArticle article, int relatedKeywordsFoundCount)
    {
        var score = 0;
        if (System.Text.RegularExpressions.Regex.IsMatch(article.FullText, "\\b\\d+[\\d,]*(?:\\.\\d+)?\\b")) score += 25;
        if (article.FullText.Contains("for example", StringComparison.OrdinalIgnoreCase) || article.FullText.Contains("example:", StringComparison.OrdinalIgnoreCase) || article.FullText.Contains("if your", StringComparison.OrdinalIgnoreCase)) score += 25;
        if (article.Sections.Any(s => s.Heading.Contains("step", StringComparison.OrdinalIgnoreCase)) || article.FullText.Contains("step 1", StringComparison.OrdinalIgnoreCase) || article.FullText.Contains("week 1", StringComparison.OrdinalIgnoreCase)) score += 25;
        if (article.Sections.Count >= 8 && relatedKeywordsFoundCount >= 5) score += 25;
        return Clamp(score);
    }

    private static int ScoreTopicAlignment(GeneratedLongformArticle article, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(primaryKeyword)) return 50;

        var keywordTerms = ExtractAlignmentTerms(primaryKeyword);
        var titleTerms = ExtractAlignmentTerms(article.Title);
        var bodyTerms = ExtractAlignmentTerms(article.FullText);
        var score = 100;

        var keywordMatches = keywordTerms.Count(term => bodyTerms.Contains(term, StringComparer.OrdinalIgnoreCase));
        if (keywordTerms.Count > 0 && keywordMatches < Math.Max(1, keywordTerms.Count / 2)) score -= 35;

        var titleMatches = titleTerms.Count(term => bodyTerms.Contains(term, StringComparer.OrdinalIgnoreCase));
        if (titleTerms.Count > 0 && titleMatches < Math.Max(1, titleTerms.Count / 2)) score -= 35;

        if (keywordTerms.Count > 0 && keywordTerms.Count(term => article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) == 0) score -= 20;

        return Clamp(score);
    }

    private static HashSet<string> ExtractAlignmentTerms(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "from", "that", "this", "your", "into", "about", "what", "when", "where", "which", "have", "will", "guide", "best", "how"
        };

        return Regex.Matches(text ?? string.Empty, "[a-zA-Z][a-zA-Z0-9-]+")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(x => x.Length >= 4 && !stopWords.Contains(x))
            .Take(12)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int CountCompletedChecklist(GeneratedLongformArticle article, string primaryKeyword)
    {
        var completed = 0;
        if (!string.IsNullOrWhiteSpace(article.Title)) completed++;
        if (!string.IsNullOrWhiteSpace(article.Slug)) completed++;
        if (!string.IsNullOrWhiteSpace(article.MetaDescription)) completed++;
        if (!string.IsNullOrWhiteSpace(primaryKeyword)) completed++;
        if (article.IntroParagraphs.Count >= 3) completed++;
        if (HasSection(article, "faq")) completed++;
        if (HasSection(article, "tool")) completed++;
        if (HasSection(article, "mistake")) completed++;
        if (HasSection(article, "step-by-step") || HasSection(article, "plan")) completed++;
        if (!string.IsNullOrWhiteSpace(article.CallToAction)) completed++;
        return completed;
    }

    private static int ScoreRange(int actual, int min, int max)
    {
        if (min <= 0 && max <= 0) return 0;
        if (actual >= min && actual <= max) return 100;
        if (actual < min) return Clamp((int)Math.Round((decimal)actual / Math.Max(1, min) * 100m));
        var overflow = actual - max;
        var penalty = Math.Min(100m, ((decimal)overflow / Math.Max(1, max)) * 100m);
        return Clamp((int)Math.Round(100m - penalty));
    }

    private static int CountWords(string value)
        => string.IsNullOrWhiteSpace(value) ? 0 : value.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static int CountPhraseOccurrences(string content, string phrase)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(phrase)) return 0;
        return Regex.Matches(content, $@"(?i)\b{Regex.Escape(phrase)}\b").Count;
    }

    private static int CountLinks(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Regex.Matches(content, @"https?://", RegexOptions.IgnoreCase).Count;

    private static int CountMedia(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Regex.Matches(content, @"!\[[^\]]*\]\([^\)]*\)|<img\b|\.(png|jpe?g|gif|webp)", RegexOptions.IgnoreCase).Count;

    private static int CountBullets(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Regex.Matches(content, @"(^|\n)\s*[-*]\s+", RegexOptions.Multiline).Count;

    private static bool HasSection(GeneratedLongformArticle article, string phrase)
        => article.Sections.Any(x => x.Heading.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static string FirstWords(string content, int wordCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return string.Join(" ", content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Take(wordCount));
    }

    private static string Slugify(string value)
        => value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static int Clamp(int value)
        => Math.Max(0, Math.Min(100, value));

    private static List<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
