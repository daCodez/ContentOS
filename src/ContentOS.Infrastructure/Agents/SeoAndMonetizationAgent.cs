using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Agents;

public interface ISeoAndMonetizationAgent
{
    Task<SeoOptimizationResult> ApplyOnPageSeoAsync(
        GeneratedLongformArticle article,
        IReadOnlyCollection<string> secondaryKeywords,
        string? optimizedSeoPackageJson = null,
        CancellationToken cancellationToken = default);

    Task<InternalLinkPlanResult> AddInternalLinkingAsync(
        IReadOnlyCollection<string> sourceSummaries,
        CancellationToken cancellationToken = default);

    Task<MonetizationPlanResult> AddCtaAndMonetizationPlacementsAsync(
        GeneratedLongformArticle article,
        CancellationToken cancellationToken = default);
}

public sealed class SeoAndMonetizationAgent : ISeoAndMonetizationAgent
{
    private readonly ILogger<SeoAndMonetizationAgent> _logger;

    // Authority domains for external linking
    private static readonly HashSet<string> AuthorityDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "consumerfinance.gov", "cfpb.gov",
        "ftc.gov",
        "statcan.gc.ca", "statcan.ca",
        "irs.gov",
        "federalreserve.gov",
        "usa.gov",
        "medlineplus.gov",
        "nih.gov",
        "budgeting.thenest.com",
        "mymoney.gov"
    };

    public SeoAndMonetizationAgent(ILogger<SeoAndMonetizationAgent> logger)
    {
        _logger = logger;
    }

    public Task<SeoOptimizationResult> ApplyOnPageSeoAsync(
        GeneratedLongformArticle article,
        IReadOnlyCollection<string> secondaryKeywords,
        string? optimizedSeoPackageJson = null,
        CancellationToken cancellationToken = default)
    {
        var keywords = secondaryKeywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToArray();

        var metaDescription = string.IsNullOrWhiteSpace(article.MetaDescription)
            ? $"{article.Title} with practical steps, realistic examples, and a clear next action."
            : article.MetaDescription;

        // === Load optimized package for primary keyword + expanded keywords ===
        string? primaryKeyword = null;
        string[]? semanticKeywords = null;
        string[]? faqKeywords = null;
        string[]? topTitles = null;

        if (!string.IsNullOrWhiteSpace(optimizedSeoPackageJson))
        {
            try
            {
                var pkg = System.Text.Json.JsonSerializer.Deserialize<OptimizedSeoPackage>(optimizedSeoPackageJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pkg is not null)
                {
                    primaryKeyword = pkg.PrimaryKeyword;
                    semanticKeywords = pkg.SemanticKeywords;
                    faqKeywords = pkg.FaqKeywords;
                    topTitles = pkg.TopTitles;

                    // Merge expanded keywords into the cluster
                    var merged = keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var k in pkg.SecondaryKeywords) merged.Add(k);
                    foreach (var k in pkg.SemanticKeywords) merged.Add(k);
                    foreach (var k in pkg.FaqKeywords) merged.Add(k);
                    keywords = merged.ToArray();
                }
            }
            catch { /* proceed without package */ }
        }

        primaryKeyword ??= keywords.FirstOrDefault() ?? article.Title;

        // === Full text for coverage analysis ===
        var combinedText = string.Join("\n", new[]
        {
            article.Title,
            article.Summary,
            string.Join("\n", article.IntroParagraphs),
            string.Join("\n", article.Sections.Select(s => s.Heading)),
            string.Join("\n", article.Sections.Select(s => s.BodyText)),
            article.BodyText,
            string.Join("\n", article.ConclusionParagraphs)
        }).ToLowerInvariant();

        var introText = string.Join("\n", article.IntroParagraphs).ToLowerInvariant();
        var headingText = string.Join("\n", article.Sections.Select(s => s.Heading)).ToLowerInvariant();

        // === 1. Keyword Placement Rules ===

        // Primary keyword checks
        var primaryLower = primaryKeyword.ToLowerInvariant();
        var keywordInTitle = article.Title.ToLowerInvariant().Contains(primaryLower);
        var keywordInIntro = introText.Contains(primaryLower);

        // Count H2s containing primary keyword
        var headingsWithPrimary = article.Sections
            .Select(s => s.Heading.ToLowerInvariant())
            .Count(h => h.Contains(primaryLower));
        var keywordInHeadings = headingsWithPrimary >= 1; // Need at least 1 H2 with primary

        // === 2. Secondary & Semantic keyword coverage ===
        var allKeywordsLower = keywords.Select(k => k.ToLowerInvariant()).ToList();
        var coveredKeywords = allKeywordsLower.Where(k => combinedText.Contains(k)).Count();
        var keywordCoverageScore = allKeywordsLower.Count > 0
            ? (int)Math.Round((decimal)coveredKeywords / allKeywordsLower.Count * 10)
            : 0;

        var missingKeywordMentions = keywords
            .Where(keyword => !combinedText.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal))
            .ToArray();

        // === 3. Heading Optimization ===
        // Each H2 should match a search intent and include a keyword variation
        var headingRecommendations = new List<string>();
        foreach (var section in article.Sections)
        {
            var hLower = section.Heading.ToLowerInvariant();
            var hasKeyword = allKeywordsLower.Any(k => hLower.Contains(k));
            if (!hasKeyword && !hLower.Contains("faq") && !hLower.Contains("conclusion"))
            {
                headingRecommendations.Add($"H2 \"{section.Heading}\" lacks a keyword variation — add a secondary or semantic keyword.");
            }
        }

        // === 4. FAQ Section ===
        var faqSection = article.Sections.FirstOrDefault(s =>
            s.Heading.ToLowerInvariant().Contains("faq") ||
            s.Heading.ToLowerInvariant().Contains("frequently asked") ||
            s.Heading.ToLowerInvariant().Contains("common questions"));

        var faqCount = faqSection?.BodyText.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*") || line.TrimStart().StartsWith("Q:"))
            ?? 0;

        var faqSectionExists = faqSection is not null && faqCount >= 3;

        // === 5. Internal Links ===
        var internalLinkCount = CountLinks(article, "internal");
        var internalLinkRecommendations = BuildInternalLinkRecommendations(article, internalLinkCount);

        // === 6. External Authority Links ===
        var externalAuthorityCount = CountLinks(article, "authority");
        var externalAuthorityLinks = BuildExternalAuthorityRecommendations(article, externalAuthorityCount);

        // === Build validation checklist ===
        var failures = new List<string>();

        if (!keywordInTitle) failures.Add("Primary keyword missing from title");
        if (!keywordInIntro) failures.Add("Primary keyword missing from intro paragraph");
        if (!keywordInHeadings) failures.Add($"Primary keyword only in {headingsWithPrimary} H2(s) — need 1-2");
        if (!faqSectionExists) failures.Add(faqSection is null ? "No FAQ section found" : $"FAQ section has only {faqCount} items — need 3+");
        if (internalLinkCount < 2) failures.Add($"Only {internalLinkCount} internal links — need 2-5");
        if (externalAuthorityCount < 1) failures.Add("No external authority links — add 1-3");
        if (keywordCoverageScore < 5) failures.Add($"Keyword coverage {keywordCoverageScore}/10 — aim for 7+");
        if (headingRecommendations.Count > article.Sections.Count / 2)
            failures.Add($"{headingRecommendations.Count} of {article.Sections.Count} headings lack keyword variations");

        // Article flows naturally: check for keyword stuffing
        var articleFlowsNaturally = !HasKeywordStuffing(combinedText, primaryLower);

        if (!articleFlowsNaturally)
            failures.Add("Possible keyword stuffing detected — reduce forced keyword repetition");

        var checklist = new SeoValidationChecklist(
            KeywordInTitle: keywordInTitle,
            KeywordInIntro: keywordInIntro,
            KeywordInHeadings: keywordInHeadings,
            FaqSectionExists: faqSectionExists,
            FaqCount: faqCount,
            InternalLinksCount: internalLinkCount,
            ExternalAuthorityLinksPresent: externalAuthorityCount >= 1,
            ArticleFlowsNaturally: articleFlowsNaturally,
            KeywordCoverageScore: keywordCoverageScore,
            Failures: failures.ToArray());

        // === Required keyword mentions (actionable for writer) ===
        var requiredKeywordMentions = keywords.Length == 0
            ? new[] { "Add at least 5 keyword variations for stronger semantic coverage." }
            : keywords.Take(10).Select(keyword =>
            {
                if (combinedText.Contains(keyword.ToLowerInvariant()))
                    return $"Keyword '{keyword}' — present ✓ (keep natural placement)";
                return $"Keyword '{keyword}' — MISSING → add naturally in a subhead, paragraph body, or FAQ";
            }).ToArray();

        var isQualitySufficient = keywordInTitle
            && keywordInIntro
            && keywordInHeadings
            && faqSectionExists
            && internalLinkCount >= 2
            && keywordCoverageScore >= 5
            && articleFlowsNaturally;

        _logger.LogInformation(
            "SEO validation: title={TitleKw} intro={IntroKw} headings={HeadKw} faq={Faq} internalLinks={Il} coverage={Cov}/10 flow={Flow} → {Pass}",
            keywordInTitle, keywordInIntro, keywordInHeadings, faqCount, internalLinkCount,
            keywordCoverageScore, articleFlowsNaturally, isQualitySufficient ? "PASS" : "FAIL");

        return Task.FromResult(new SeoOptimizationResult(
            article.Title,
            metaDescription,
            keywords,
            "Structure the draft to satisfy the dominant search intent with a beginner-friendly, actionable answer.",
            requiredKeywordMentions,
            missingKeywordMentions,
            internalLinkRecommendations,
            externalAuthorityLinks,
            article.EstimatedWordCount,
            article.Sections.Select(s => s.Heading).ToArray(),
            isQualitySufficient,
            checklist));
    }

    public Task<InternalLinkPlanResult> AddInternalLinkingAsync(
        IReadOnlyCollection<string> sourceSummaries,
        CancellationToken cancellationToken = default)
    {
        var summaries = sourceSummaries
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Take(5)
            .ToArray();

        var anchorDefaults = new[]
        {
            "how to save money fast",
            "how to cut expenses",
            "emergency fund guide",
            "simple monthly budget template",
            "budgeting for beginners"
        };

        var links = summaries
            .Select((summary, index) => (object)new
            {
                anchor = anchorDefaults[Math.Min(index, anchorDefaults.Length - 1)],
                target = summary,
                placement = index == 0 ? "Intro or first tactical section"
                          : index == 1 ? "Middle of article"
                          : index == 2 ? "FAQ section"
                          : index == 3 ? "Related tools section"
                          : "Conclusion or resources"
            })
            .ToArray();

        var recommendedAnchors = links
            .Select(link => link.GetType().GetProperty("anchor")?.GetValue(link)?.ToString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return Task.FromResult(new InternalLinkPlanResult(links, recommendedAnchors, links.Length >= 2));
    }

    public Task<MonetizationPlanResult> AddCtaAndMonetizationPlacementsAsync(
        GeneratedLongformArticle article,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // No verified offer inventory is supplied to this boundary. Retain the actual
        // next action rather than inventing a download, affiliate offer or paid tool.
        var toolRecommendations = Array.Empty<string>();
        var faqQuestions = Array.Empty<string>();

        return Task.FromResult(new MonetizationPlanResult(
            new[] { "Retain the article's existing next action. No verified monetization offer was supplied." },
            article.CallToAction,
            toolRecommendations,
            faqQuestions,
            article.Sections.FirstOrDefault(s => s.Heading.Contains("tool", StringComparison.OrdinalIgnoreCase))?.Heading ?? string.Empty,
            !string.IsNullOrWhiteSpace(article.CallToAction)));
    }

    // === Helper: Count links in article text ===

    private static int CountLinks(GeneratedLongformArticle article, string linkType)
    {
        var fullText = string.Join("\n", article.IntroParagraphs)
            + "\n" + string.Join("\n", article.Sections.Select(s => s.BodyText))
            + "\n" + string.Join("\n", article.ConclusionParagraphs);

        if (linkType == "authority")
        {
            var count = 0;
            foreach (var domain in AuthorityDomains)
            {
                if (fullText.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            // Also count generic markdown links with https
            var httpsLinks = System.Text.RegularExpressions.Regex.Matches(fullText, @"https?://[^\s\)]+");
            count += httpsLinks.Count(m => !m.Value.Contains("example.com", StringComparison.OrdinalIgnoreCase));
            return count;
        }

        // Internal links: look for relative paths or same-site references
        var internalPatterns = new[] { "/articles/", "/blog/", "/guides/", "/how-to/", "[internal]", "→" };
        var internalCount = 0;
        foreach (var pattern in internalPatterns)
        {
            if (fullText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                internalCount++;
        }

        return internalCount;
    }

    // === Helper: Build internal link recommendations ===

    private static string[] BuildInternalLinkRecommendations(GeneratedLongformArticle article, int currentCount)
    {
        var recommendations = new List<string>
        {
            "Link to your pillar budgeting guide near the first action step.",
            "Link to a worksheet, template, or checklist from the tools section.",
            "Link to a related beginner guide from the FAQ section."
        };

        if (currentCount < 2)
        {
            recommendations.Insert(0, $"⚠️ Only {currentCount} internal link(s) detected — aim for 2-5 per article.");
        }

        if (article.Sections.Any(s => s.Heading.ToLowerInvariant().Contains("tool")))
        {
            recommendations.Add("Link to detailed tool reviews from the tools section.");
        }

        if (article.Sections.Any(s => s.Heading.ToLowerInvariant().Contains("mistake")))
        {
            recommendations.Add("Link to a 'common mistakes' related post from the mistakes section.");
        }

        return recommendations.ToArray();
    }

    // === Helper: Build external authority recommendations ===

    private static string[] BuildExternalAuthorityRecommendations(GeneratedLongformArticle article, int currentCount)
    {
        var recommendations = new List<string>
        {
            "Consumer Financial Protection Bureau (consumerfinance.gov) — for budgeting rules and consumer rights",
            "Federal Trade Commission (ftc.gov) — for scam warnings and consumer protection",
            "IRS.gov — for tax-related budgeting and withholding guidance"
        };

        if (currentCount < 1)
        {
            recommendations.Insert(0, "⚠️ No external authority links detected — add 1-3 trusted .gov or .org sources.");
        }

        return recommendations.ToArray();
    }

    // === Helper: Detect keyword stuffing ===

    private static bool HasKeywordStuffing(string text, string primaryLower)
    {
        if (string.IsNullOrWhiteSpace(primaryLower) || primaryLower.Length < 4)
            return false;

        // Count occurrences of primary keyword
        var count = 0;
        var pos = 0;
        while ((pos = text.IndexOf(primaryLower, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += primaryLower.Length;
        }

        // Stuffing = more than ~4 mentions per 1000 words
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount == 0) return false;

        var mentionsPer1000 = (double)count / wordCount * 1000;
        return mentionsPer1000 > 6; // More than 6 mentions per 1000 words = stuffing
    }
}
