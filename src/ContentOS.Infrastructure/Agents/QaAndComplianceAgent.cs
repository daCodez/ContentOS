using ContentOS.Domain.Entities;
using ContentOS.Domain.Qa;

namespace ContentOS.Infrastructure.Agents;

public interface IQaAndComplianceAgent
{
    Task<QaReportResult> RunFinalQaReviewAsync(ContentIdea? idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default);
}

/// <summary>Applies deterministic article quality rules, including known filler and source-contamination defects.</summary>
public sealed class QaAndComplianceAgent : IQaAndComplianceAgent
{
    /// <summary>Checks article structure and deterministic content defects before publication.</summary>
    /// <remarks>FAQ approval requires a question followed by non-question answer text; these structural checks do not establish factual accuracy.</remarks>
    /// <param name="idea">The approved topic and search keyword.</param>
    /// <param name="article">The complete structured draft.</param>
    /// <param name="secondaryKeywords">Approved supporting phrases.</param>
    /// <param name="sourceSummaries">Available research context.</param>
    /// <param name="cancellationToken">Cancellation requested by the caller.</param>
    /// <returns>A report whose quality flag is false whenever a hard content rule fails.</returns>
    public Task<QaReportResult> RunFinalQaReviewAsync(ContentIdea? idea, GeneratedLongformArticle article, IReadOnlyCollection<string> secondaryKeywords, IReadOnlyCollection<string> sourceSummaries, CancellationToken cancellationToken = default)
    {
        var keywordVariations = secondaryKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var hardRuleFailures = new List<string>();
        var keywordWarnings = new List<string>();

        if (article.IntroParagraphs.Count < 3)
            hardRuleFailures.Add("Hook must appear in the first 3 lines.");

        if (!article.Sections.Any(s => IsFaqHeading(s.Heading)))
            hardRuleFailures.Add("FAQ section is required.");

        else if (!article.Sections.Where(s => IsFaqHeading(s.Heading))
            .Select(s => string.Join("\n", s.Paragraphs)).Any(HasQuestionAndAnswer))
            hardRuleFailures.Add("FAQ must contain an actual question and answer, not only a heading or generic tips.");

        var repeatedParagraph = article.Sections.SelectMany(s => s.Paragraphs)
            .Select(p => System.Text.RegularExpressions.Regex.Replace(p.Trim(), @"\s+", " "))
            .Where(p => p.Length >= 60)
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);
        if (repeatedParagraph)
            hardRuleFailures.Add("Article contains a repeated paragraph. Give each section distinct, useful content.");

        if (System.Text.RegularExpressions.Regex.IsMatch(article.FullText,
            @"menu list icon|trend unchanged icon|arrow up icon", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            hardRuleFailures.Add("Article contains navigation or icon labels copied from source UI.");

        if (!article.Sections.Any(s => s.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase)))
            hardRuleFailures.Add("Monetization / tools section is required.");

        if (string.IsNullOrWhiteSpace(article.CallToAction))
            hardRuleFailures.Add("CTA is required.");

        if (!article.FullText.Contains("- ", StringComparison.Ordinal) && !article.FullText.Contains("1. ", StringComparison.Ordinal))
            hardRuleFailures.Add("Skimmable formatting requires bullet points or numbered list formatting.");

        if (!article.Sections.Any(s => IsLearnHeading(s.Heading)))
            hardRuleFailures.Add("A 'What You'll Learn' section is required after the intro.");

        // --- Keyword repetition / stuffing check ---
        if (!string.IsNullOrWhiteSpace(idea?.PrimaryKeyword))
        {
            var exactKeyword = idea!.PrimaryKeyword.Trim();
            var titleHasExactKeyword = !string.IsNullOrWhiteSpace(article.Title) &&
                article.Title.Contains(exactKeyword, StringComparison.OrdinalIgnoreCase);
            var exactCount = System.Text.RegularExpressions.Regex.Matches(article.FullText, System.Text.RegularExpressions.Regex.Escape(exactKeyword), System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            var headingExactCount = article.Sections.Count(s =>
                !string.IsNullOrWhiteSpace(s.Heading) && s.Heading.Contains(exactKeyword, StringComparison.OrdinalIgnoreCase));

            var weakVariation = keywordVariations.Length < 3;
            var clearlyStuffed = exactCount >= (titleHasExactKeyword ? 6 : 7);
            var unnaturallyRepeated = exactCount >= (titleHasExactKeyword ? 4 : 5) && weakVariation;
            var repeatedAcrossHeadings = headingExactCount > 1;

            if (repeatedAcrossHeadings)
            {
                hardRuleFailures.Add($"Primary keyword '{exactKeyword}' appears in multiple headings. Use natural heading variations instead.");
            }

            if (clearlyStuffed || unnaturallyRepeated)
            {
                hardRuleFailures.Add($"Primary keyword '{exactKeyword}' is repeated unnaturally and reads like keyword stuffing. Reduce exact-match repetition and use more natural variations.");
            }
            else
            {
                if (titleHasExactKeyword && exactCount > 3)
                {
                    keywordWarnings.Add($"Primary keyword '{exactKeyword}' appears {exactCount} times. Since it is already in the title, body usage should generally stay around 2 to 3 exact matches.");
                }
                else if (!titleHasExactKeyword && exactCount > 4)
                {
                    keywordWarnings.Add($"Primary keyword '{exactKeyword}' appears {exactCount} times. Consider reducing exact-match repetition.");
                }

                if (weakVariation)
                {
                    keywordWarnings.Add("Semantic keyword variation is weak. Add more natural phrasing and synonyms.");
                }
            }
        }

        // --- Quick Start / cheat sheet check ---
        var hasQuickStart = article.Sections.Any(s =>
            s.Heading.Contains("Quick Start", StringComparison.OrdinalIgnoreCase) ||
            s.Heading.Contains("Cheat Sheet", StringComparison.OrdinalIgnoreCase) ||
            s.Heading.Contains("Quick Reference", StringComparison.OrdinalIgnoreCase));

        // --- Wall-of-text check (body sections, not just intros) ---
        var wallOfTextSections = article.Sections
            .Where(s => s.Paragraphs.Any(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 40))
            .Select(s => s.Heading)
            .Take(3)
            .ToList();

        // --- Personality / conversational tone check ---
        var hasPersonality = article.FullText.Contains("...", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("yeah", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("this is where", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("here's the thing", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("the truth is", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("most people", StringComparison.OrdinalIgnoreCase) ||
                             article.FullText.Contains("you're not alone", StringComparison.OrdinalIgnoreCase);

        // --- Meta-commentary check (writing about the article instead of to the reader) ---
        var hasMetaCommentary = article.FullText.Contains("this section should", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("the article should", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("this guide will", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("reduces decision fatigue", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("narrowing the field", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("the next move is to", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("this section can also", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("a useful longform article", StringComparison.OrdinalIgnoreCase) ||
                                article.FullText.Contains("in the research layer", StringComparison.OrdinalIgnoreCase);

        if (hasMetaCommentary)
            hardRuleFailures.Add("Article contains meta-commentary (writing about the article instead of to the reader). Replace phrases like 'this section should' or 'this guide will' with direct, practical content.");

        // --- Utility checks ---
        var hasNumbers = System.Text.RegularExpressions.Regex.IsMatch(article.FullText, "\\b\\d+[\\d,]*(?:\\.\\d+)?\\b");
        var hasConcreteExample = article.FullText.Contains("for example", StringComparison.OrdinalIgnoreCase) ||
                                 article.FullText.Contains("example:", StringComparison.OrdinalIgnoreCase) ||
                                 article.FullText.Contains("if you earn", StringComparison.OrdinalIgnoreCase) ||
                                 article.FullText.Contains("if your income", StringComparison.OrdinalIgnoreCase) ||
                                 HasNamedCalculatedExample(article.Sections);
        var hasStepPlan = article.Sections.Any(s => s.Heading.Contains("step", StringComparison.OrdinalIgnoreCase) || s.Heading.Contains("quick start", StringComparison.OrdinalIgnoreCase)) ||
                          article.FullText.Contains("week 1", StringComparison.OrdinalIgnoreCase) ||
                          article.FullText.Contains("week 2", StringComparison.OrdinalIgnoreCase);
        var titleTerms = ExtractAlignmentTerms(article.Title);
        var keywordTerms = ExtractAlignmentTerms(idea?.PrimaryKeyword ?? string.Empty);
        var articleTerms = ExtractAlignmentTerms(article.FullText);
        var titleKeywordAligned = !keywordTerms.Any() || keywordTerms.Count(term => article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) >= Math.Max(1, Math.Min(2, keywordTerms.Count));
        var bodySupportsKeyword = !keywordTerms.Any() || keywordTerms.Count(term => articleTerms.Contains(term, StringComparer.OrdinalIgnoreCase)) >= Math.Max(1, keywordTerms.Count / 2);
        var titleSupportsBody = !titleTerms.Any() || titleTerms.Count(term => articleTerms.Contains(term, StringComparer.OrdinalIgnoreCase)) >= Math.Max(1, titleTerms.Count / 2);

        if (!hasNumbers)
            hardRuleFailures.Add("Article contains zero real numbers. Add savings amounts, budgets, timelines, or other concrete figures.");

        if (!hasConcreteExample)
            hardRuleFailures.Add("Article contains no worked example. Add at least one concrete example with realistic numbers.");

        if (!hasStepPlan)
            hardRuleFailures.Add("Article contains no real step-by-step action plan. Add a followable weekly or staged plan.");

        if (!titleKeywordAligned)
            hardRuleFailures.Add("Title and primary keyword intent are misaligned. The title should naturally support the primary search intent.");

        if (!bodySupportsKeyword)
            hardRuleFailures.Add("Article topic does not naturally support the primary keyword. Rewrite toward the actual topic instead of forcing the keyword.");

        if (!titleSupportsBody)
            hardRuleFailures.Add("Title promise does not match the article body. Rewrite so the article actually delivers what the title promises.");

        var hookScore = article.IntroParagraphs.Count >= 3 ? 9m : 5m;
        var seoScore = keywordVariations.Length >= 3 && !string.IsNullOrWhiteSpace(article.MetaDescription) ? 8.5m : 6m;
        var readabilityScore = article.IntroParagraphs.Take(3).All(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 18) ? 8.5m : 7m;
        var valueScore = article.Sections.Count >= 8 && article.EstimatedWordCount >= 1800 ? 8.5m : 7m;
        if (!hasNumbers) valueScore = Math.Min(valueScore, 5m);
        if (!hasConcreteExample) valueScore = Math.Min(valueScore, 5m);
        if (!hasStepPlan) valueScore = Math.Min(valueScore, 5.5m);
        if (!titleKeywordAligned || !bodySupportsKeyword || !titleSupportsBody) seoScore = Math.Min(seoScore, 4.5m);
        var monetizationScore = article.Sections.Any(s => s.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(article.CallToAction) ? 8.5m : 6m;

        // --- Penalize keyword stuffing ---
        if (!string.IsNullOrWhiteSpace(idea?.PrimaryKeyword))
        {
            var exactKeyword = idea!.PrimaryKeyword.Trim();
            var exactCount = System.Text.RegularExpressions.Regex.Matches(article.FullText, System.Text.RegularExpressions.Regex.Escape(exactKeyword), System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            var introExactCount = article.IntroParagraphs.Sum(p => System.Text.RegularExpressions.Regex.Matches(p, System.Text.RegularExpressions.Regex.Escape(exactKeyword), System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count);
            var headingExactCount = article.Sections.Count(s => s.Heading.Contains(exactKeyword, StringComparison.OrdinalIgnoreCase));
            if (exactCount > 4 || introExactCount > 1 || headingExactCount > 2)
                seoScore = Math.Min(seoScore, 5m);
        }

        // --- Penalize wall of text ---
        if (wallOfTextSections.Count > 0)
            readabilityScore = Math.Min(readabilityScore, 6.5m);

        // --- Penalize lack of personality ---
        if (!hasPersonality)
            readabilityScore = Math.Min(readabilityScore, 7m);

        // --- Bonus for Quick Start section ---
        if (hasQuickStart)
            valueScore = Math.Min(valueScore + 0.5m, 10m);

        var overallScore = Math.Round((hookScore + seoScore + readabilityScore + valueScore + monetizationScore) / 5m, 1);
        var rewriteRequired = hardRuleFailures.Count > 0 || overallScore < 8m;
        var passed = !article.IsSynthetic && article.MeetsMinimumQuality && !string.IsNullOrWhiteSpace(article.Slug) && !rewriteRequired;

        QaReworkDirective? reworkDirective = null;
        if (rewriteRequired)
        {
            reworkDirective = BuildReworkDirective(article, idea, keywordVariations, keywordWarnings, hardRuleFailures, hookScore, seoScore, readabilityScore, valueScore, monetizationScore, overallScore, hasPersonality, hasQuickStart, wallOfTextSections, hasMetaCommentary, hasNumbers, hasConcreteExample, hasStepPlan);
        }

        return Task.FromResult(new QaReportResult(
            passed ? "Pass" : "Blocked",
            hookScore, seoScore, readabilityScore, valueScore, monetizationScore,
            overallScore,
            rewriteRequired,
            hardRuleFailures.ToArray(),
            new[]
            {
                rewriteRequired ? "Targeted rework needed — see ReworkDirective for specific fixes." : "Draft cleared the scoring gate.",
                $"Word count: {article.EstimatedWordCount}",
                $"Sections: {article.Sections.Count}",
                $"Intent: {(string.IsNullOrWhiteSpace(idea?.SearchIntent) ? "not provided" : idea!.SearchIntent)}",
                $"Synthetic fallback: {article.IsSynthetic}"
            },
            article.IsSynthetic,
            passed,
            reworkDirective));
    }

    private static QaReworkDirective BuildReworkDirective(
        GeneratedLongformArticle article,
        ContentIdea? idea,
        string[] keywordVariations,
        List<string> keywordWarnings,
        List<string> hardRuleFailures,
        decimal hookScore,
        decimal seoScore,
        decimal readabilityScore,
        decimal valueScore,
        decimal monetizationScore,
        decimal overallScore,
        bool hasPersonality,
        bool hasQuickStart,
        List<string> wallOfTextSections,
        bool hasMetaCommentary,
        bool hasNumbers,
        bool hasConcreteExample,
        bool hasStepPlan)
    {
        var requiredFixes = new List<string>();
        var sectionsToRewrite = new List<string>();
        var sectionsToPreserve = new List<string>();
        var seoFixes = new List<string>();
        var monetizationFixes = new List<string>();
        var readabilityFixes = new List<string>();
        var humanizationFixes = new List<string>();
        var fixItems = new List<QaFixItem>();
        string? replacementHookGuidance = null;
        string? replacementCtaGuidance = null;

        // --- Sections to preserve (already passing) ---
        foreach (var section in article.Sections)
        {
            var heading = section.Heading;
            var isFailing = false;

            if (IsFaqHeading(heading) &&
                hardRuleFailures.Any(f => f.Contains("FAQ")))
                isFailing = true;

            if (heading.Contains("Tool", StringComparison.OrdinalIgnoreCase) &&
                hardRuleFailures.Any(f => f.Contains("Monetization") || f.Contains("tools section", StringComparison.OrdinalIgnoreCase)))
                isFailing = true;

            if (heading.Contains("What You", StringComparison.OrdinalIgnoreCase) &&
                hardRuleFailures.Any(f => f.Contains("What You'll Learn")))
                isFailing = true;

            if (isFailing)
                sectionsToRewrite.Add(heading);
            else
                sectionsToPreserve.Add(heading);
        }

        // --- Hook ---
        if (hookScore < 7m)
        {
            sectionsToRewrite.Insert(0, "Intro / Hook");
            replacementHookGuidance = $"Current hook score: {hookScore}/10. Rewrite the first 3 intro paragraphs. Open with a visceral, specific pain the reader recognizes immediately. Then 1 line of relatable reality. Then 1 line of simple promise. Each paragraph: max 18 words.";
            requiredFixes.Add("Rewrite intro hook to score ≥7/10");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-hook-01",
                Section = "Intro / Hook",
                Description = "Rewrite first 3 intro paragraphs as pain/reality/promise hook, each ≤18 words",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Hook
            });
        }

        // --- Missing sections ---
        if (!article.Sections.Any(s => IsFaqHeading(s.Heading)))
        {
            requiredFixes.Add("Add a FAQ section with 3-5 practical questions and specific answers");
            sectionsToRewrite.Add("FAQ");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-faq-01",
                Section = "FAQ",
                Description = "Add FAQ section with 3-5 practical questions and specific answers",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Structural
            });
        }

        if (!article.Sections.Any(s => s.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase)))
        {
            requiredFixes.Add("Add a Tools section with 2-3 specific tool recommendations (name, best for, 1 pro, 1 con)");
            sectionsToRewrite.Add("Tools");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-tools-01",
                Section = "Tools",
                Description = "Add Tools section with 2-3 tool recommendations (name, best for, 1 pro, 1 con)",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Monetization
            });
        }

        if (!article.Sections.Any(s => s.Heading.Contains("What You", StringComparison.OrdinalIgnoreCase)))
        {
            requiredFixes.Add("Add a 'What You'll Learn' section after the intro using bullet points");
            sectionsToRewrite.Add("What You'll Learn");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-wyl-01",
                Section = "What You'll Learn",
                Description = "Add 'What You'll Learn' section after intro with bullet points",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Structural
            });
        }

        // --- SEO fixes ---
        if (seoScore < 7m)
        {
            var missingKws = keywordVariations
                .Where(k => !article.FullText.Contains(k, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            if (missingKws.Count > 0)
            {
                seoFixes.Add($"Weave these missing keywords naturally into relevant sections: {string.Join(", ", missingKws)}");
                requiredFixes.Add($"Add {missingKws.Count} missing keyword variations");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-seo-kw-01",
                    Section = "Multiple sections",
                    Description = $"Weave missing keywords naturally: {string.Join(", ", missingKws)}",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Seo
                });
            }

            if (string.IsNullOrWhiteSpace(article.MetaDescription))
            {
                seoFixes.Add("Write a metaDescription (150-160 chars) with primary keyword and action promise");
                requiredFixes.Add("Add meta description");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-seo-meta-01",
                    Section = "Meta",
                    Description = "Write metaDescription (150-160 chars) with primary keyword and action promise",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Seo
                });
            }

            sectionsToRewrite.Add("SEO coverage");
        }

        // --- Keyword stuffing and synonym coverage ---
        if (!string.IsNullOrWhiteSpace(idea?.PrimaryKeyword))
        {
            var exactKeyword = idea!.PrimaryKeyword.Trim();
            var exactCount = System.Text.RegularExpressions.Regex.Matches(article.FullText, System.Text.RegularExpressions.Regex.Escape(exactKeyword), System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            if (exactCount > 4)
            {
                seoFixes.Add($"Primary keyword '{exactKeyword}' appears {exactCount} times — reduce forced exact repetition and use natural variations instead.");
                requiredFixes.Add("Reduce keyword stuffing — use natural variations instead of exact repetition");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-seo-stuffing-01",
                    Section = "Multiple sections",
                    Description = $"Reduce exact keyword '{exactKeyword}' from {exactCount} to a lighter natural usage pattern.",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Seo
                });
            }

            if (keywordVariations.Length < 5)
            {
                seoFixes.Add("Add more natural keyword variations and synonyms so the article covers the topic without repeating the primary phrase.");
                requiredFixes.Add("Improve synonym coverage with 5 to 10 natural variations");
            }
        }

        // --- Readability fixes ---
        if (readabilityScore < 7.5m)
        {
            var longIntros = article.IntroParagraphs.Take(3)
                .Count(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 18);

            if (longIntros > 0)
            {
                readabilityFixes.Add($"Shorten {longIntros} intro paragraph(s) to ≤18 words each");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-read-intro-01",
                    Section = "Intro / Hook",
                    Description = $"Shorten {longIntros} intro paragraph(s) to ≤18 words each",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Readability
                });
            }

            readabilityFixes.Add("Break paragraphs longer than 3 sentences into shorter paragraphs or bullets");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-read-paras-01",
                Section = "All sections",
                Description = "Break paragraphs longer than 3 sentences into shorter paragraphs or bullets",
                Priority = QaFixPriority.Minor,
                Category = QaFixCategory.Readability
            });

            readabilityFixes.Add("Scan all sections for walls of text — add bullets or numbered lists where they improve scannability");
            requiredFixes.Add("Improve readability score");
        }

        // --- Wall of text (body sections) ---
        if (wallOfTextSections.Count > 0)
        {
            foreach (var sectionName in wallOfTextSections)
            {
                readabilityFixes.Add($"Section '{sectionName}' has paragraphs over 40 words — break into bullets or short paragraphs");
                fixItems.Add(new QaFixItem
                {
                    Id = $"fix-wall-{sectionName.GetHashCode():X}",
                    Section = sectionName,
                    Description = "Break long paragraphs into 2-3 sentence chunks with bullets",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Readability
                });
            }
            requiredFixes.Add("Break up wall-of-text sections");
        }

        // --- Personality / conversational tone ---
        if (!hasPersonality)
        {
            readabilityFixes.Add("Add 2-3 conversational personality moments (e.g., 'This is where most people mess up.', 'Yeah… this one hurts.')");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-personality-01",
                Section = "Middle sections",
                Description = "Add 2-3 conversational personality moments to break up generic tone",
                Priority = QaFixPriority.Minor,
                Category = QaFixCategory.Humanization
            });
        }

        // --- Meta-commentary (writing about the article instead of to the reader) ---
        if (hasMetaCommentary)
        {
            readabilityFixes.Add("Remove all meta-commentary — replace 'this section should' / 'this guide will' / 'the article should' with direct, practical content that speaks TO the reader");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-meta-01",
                Section = "All sections",
                Description = "Remove meta-commentary. Talk TO the reader, not ABOUT the article.",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Humanization
            });
        }

        // --- Quick Start section ---
        if (!hasQuickStart)
        {
            requiredFixes.Add("Add a Quick Start or Cheat Sheet section near the top for readers who want fast answers");
            sectionsToRewrite.Add("Quick Start");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-quickstart-01",
                Section = "Quick Start",
                Description = "Add a scannable Quick Start section with a table or bullet cheat sheet near the top",
                Priority = QaFixPriority.Major,
                Category = QaFixCategory.Structural
            });
        }

        // --- Formatting ---
        if (hardRuleFailures.Any(f => f.Contains("bullet points") || f.Contains("Skimmable")))
        {
            readabilityFixes.Add("Add bullet lists or numbered steps to at least 3 sections. Step-by-Step must use numbered steps. Tips and Mistakes should use bullets.");
            requiredFixes.Add("Add skimmable formatting");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-fmt-bullets-01",
                Section = "Multiple sections",
                Description = "Add bullet lists or numbered steps to at least 3 sections",
                Priority = QaFixPriority.Major,
                Category = QaFixCategory.Formatting
            });
        }

        // --- Utility hard-fail rewrites ---
        if (!hasNumbers)
        {
            requiredFixes.Add("Add real numbers like savings targets, budgets, or timelines");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-utility-numbers-01",
                Section = "Multiple sections",
                Description = "Add concrete numbers such as dollar amounts, timelines, or budget examples",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Structural
            });
        }

        if (!hasConcreteExample)
        {
            requiredFixes.Add("Add at least one worked example with realistic numbers");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-utility-example-01",
                Section = "Relevant body section",
                Description = "Add a worked example the reader can copy or adapt",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Structural
            });
        }

        if (!hasStepPlan)
        {
            requiredFixes.Add("Add a real step-by-step plan with staged or weekly actions");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-utility-plan-01",
                Section = "Quick Start or Step-by-Step",
                Description = "Add a followable staged plan with concrete actions",
                Priority = QaFixPriority.Critical,
                Category = QaFixCategory.Structural
            });
        }

        // --- Monetization fixes ---
        if (monetizationScore < 7m)
        {
            if (string.IsNullOrWhiteSpace(article.CallToAction))
            {
                replacementCtaGuidance = "Write a specific, actionable CTA. Not generic. Example: 'Download the simple budget template, then schedule one 20-minute money date this week.'";
                requiredFixes.Add("Add specific CallToAction");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-cta-01",
                    Section = "CTA",
                    Description = "Add specific, actionable CallToAction (not generic)",
                    Priority = QaFixPriority.Critical,
                    Category = QaFixCategory.Cta
                });
            }

            monetizationFixes.Add("Add or strengthen tools section with specific recommendations");
            requiredFixes.Add("Improve monetization score");
        }

        // --- Value / depth ---
        if (valueScore < 7.5m)
        {
            if (article.Sections.Count < 8)
            {
                requiredFixes.Add($"Add {8 - article.Sections.Count} more section(s) to reach minimum 8");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-depth-sec-01",
                    Section = "Structure",
                    Description = $"Add {8 - article.Sections.Count} more section(s) to reach minimum 8",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Structural
                });
            }
            if (article.EstimatedWordCount < 1800)
            {
                requiredFixes.Add($"Expand from {article.EstimatedWordCount} to at least 1800 words — add depth to thinnest sections with concrete examples and real scenarios");
                fixItems.Add(new QaFixItem
                {
                    Id = "fix-depth-words-01",
                    Section = "Thinnest sections",
                    Description = $"Expand from {article.EstimatedWordCount} to ≥1800 words with concrete examples",
                    Priority = QaFixPriority.Major,
                    Category = QaFixCategory.Structural
                });
            }
        }

        // --- Humanization hints ---
        if (article.IsSynthetic || readabilityScore < 7m)
        {
            humanizationFixes.Add("Remove robotic phrasing, repetitive sentence structures, and corporate filler");
            humanizationFixes.Add("Vary sentence length and structure to sound natural");
            fixItems.Add(new QaFixItem
            {
                Id = "fix-human-01",
                Section = "All sections",
                Description = "Remove robotic phrasing and vary sentence structure",
                Priority = QaFixPriority.Minor,
                Category = QaFixCategory.Humanization
            });
        }

        // --- Determine if full rewrite is needed ---
        var missingSectionCount = new[] { "FAQ", "Tool", "What You'll Learn" }
            .Count(name => !article.Sections.Any(s => s.Heading.Contains(name, StringComparison.OrdinalIgnoreCase)));
        var requiresFullRewrite = missingSectionCount >= 3 && overallScore < 6m;

        // --- Max rewrite scope ---
        var rewriteCount = sectionsToRewrite.Distinct().Count();
        var totalCount = Math.Max(1, article.Sections.Count);
        var maxScope = requiresFullRewrite ? 100m : Math.Round(Math.Min(100m, (decimal)rewriteCount / totalCount * 100m + 15m), 0);

        // --- Build summary ---
        var summary = requiresFullRewrite
            ? "STRUCTURAL_REWORK: Multiple required sections missing and low scores. Keep good sections, add missing ones, fix weak areas."
            : missingSectionCount >= 2
            ? "TARGETED_EXPANSION: Add missing sections and strengthen weak areas. Preserve working content."
            : "TARGETED_FIX: Address failed rubric items only. Rewrite target sections only. Preserve passing content. Use no forbidden bad patterns.";

        // --- Before/after diff summary ---
        var beforeAfter = BuildBeforeAfterSummary(article, sectionsToPreserve, sectionsToRewrite, hardRuleFailures, overallScore);

        requiredFixes.AddRange(keywordWarnings);

        return new QaReworkDirective
        {
            AttemptNumber = 1, // Overwritten by coordinator
            Summary = summary,
            RequiredFixes = requiredFixes,
            HardRuleFailures = hardRuleFailures.ToArray(),
            SectionsToRewrite = sectionsToRewrite.Distinct().ToArray(),
            SectionsToPreserve = sectionsToPreserve.ToArray(),
            Focus = new QaRewriteFocus
            {
                SeoFixes = seoFixes,
                MonetizationFixes = monetizationFixes,
                ReadabilityFixes = readabilityFixes,
                HumanizationFixes = humanizationFixes,
                ReplacementHookGuidance = replacementHookGuidance,
                ReplacementCtaGuidance = replacementCtaGuidance
            },
            RequiresFullRewrite = requiresFullRewrite,
            MaxRewriteScopePercent = maxScope,
            FixItems = fixItems,
            AdditionalWriterInstructions = BuildAdditionalInstructions(requiredFixes, sectionsToPreserve, sectionsToRewrite, maxScope),
            ValidationChecks = BuildValidationChecks(hardRuleFailures, seoFixes, monetizationFixes, readabilityFixes, sectionsToRewrite, article, keywordVariations),
            ForbiddenPatterns = BuildForbiddenPatterns(hardRuleFailures),
            ExemplarPatterns = new[]
            {
                "Answer the question quickly.",
                "Use concrete numbers or thresholds.",
                "Include worked examples or scenarios.",
                "Use explicit steps or a staged plan.",
                "Keep advice tied to real constraints."
            },
            BeforeAfterSummary = beforeAfter
        };
    }

    private static string BuildBeforeAfterSummary(
        GeneratedLongformArticle article,
        List<string> sectionsToPreserve,
        List<string> sectionsToRewrite,
        List<string> hardRuleFailures,
        decimal overallScore)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEFORE (current state):");
        sb.AppendLine($"  Sections: {article.Sections.Count} total — {string.Join(", ", article.Sections.Select(s => s.Heading))}");
        sb.AppendLine($"  Word count: {article.EstimatedWordCount}");
        sb.AppendLine($"  Overall score: {overallScore}/10");
        sb.AppendLine($"  Hard failures: {hardRuleFailures.Count}");
        sb.AppendLine();
        sb.AppendLine("AFTER (expected state):");
        sb.AppendLine($"  Preserved sections (copy as-is): {sectionsToPreserve.Count}");
        foreach (var s in sectionsToPreserve)
            sb.AppendLine($"    ✓ {s}");
        sb.AppendLine($"  Rewritten/added sections: {sectionsToRewrite.Distinct().Count()}");
        foreach (var s in sectionsToRewrite.Distinct())
            sb.AppendLine($"    ✎ {s}");
        sb.AppendLine($"  Expected score improvement: {overallScore} → ≥8.0");
        return sb.ToString().TrimEnd();
    }

    private static List<string> BuildValidationChecks(
        List<string> hardRuleFailures,
        List<string> seoFixes,
        List<string> monetizationFixes,
        List<string> readabilityFixes,
        List<string> sectionsToRewrite,
        GeneratedLongformArticle article,
        string[] keywordVariations)
    {
        var checks = new List<string>();

        if (hardRuleFailures.Any(f => f.Contains("FAQ")))
            checks.Add("Article contains a FAQ section with at least 3 questions");
        if (hardRuleFailures.Any(f => f.Contains("Tool") || f.Contains("Monetization")))
            checks.Add("Article contains a Tools section with specific tool recommendations");
        if (hardRuleFailures.Any(f => f.Contains("What You")))
            checks.Add("Article has a 'What You'll Learn' section after the intro");
        if (hardRuleFailures.Any(f => f.Contains("Hook") || f.Contains("3 lines")))
            checks.Add("First 3 intro paragraphs each work as hook text (pain, reality, promise)");
        if (hardRuleFailures.Any(f => f.Contains("CTA")))
            checks.Add("Article has a specific, actionable CallToAction at the end");
        if (hardRuleFailures.Any(f => f.Contains("bullet") || f.Contains("Skimmable")))
            checks.Add("At least 3 sections contain bullet points or numbered steps");
        if (hardRuleFailures.Any(f => f.Contains("keyword stuffing", StringComparison.OrdinalIgnoreCase) ||
                                      f.Contains("multiple headings", StringComparison.OrdinalIgnoreCase) ||
                                      f.Contains("robotic", StringComparison.OrdinalIgnoreCase)))
            checks.Add("Primary keyword usage feels natural, is not repeated across multiple headings, and does not read like keyword stuffing.");

        if (seoFixes.Count > 0)
        {
            var missingKws = keywordVariations.Where(k => !article.FullText.Contains(k, StringComparison.OrdinalIgnoreCase)).Take(3);
            foreach (var kw in missingKws)
                checks.Add($"Keyword variation '{kw}' appears naturally in the article text");
            if (string.IsNullOrWhiteSpace(article.MetaDescription))
                checks.Add("metaDescription is present and includes the primary keyword");
        }

        if (monetizationFixes.Count > 0)
        {
            if (!article.Sections.Any(s => s.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase)))
                checks.Add("Tools section lists 2-3 tools with: name, best for, 1 pro, 1 con");
            if (string.IsNullOrWhiteSpace(article.CallToAction))
                checks.Add("CTA is specific and actionable (not generic)");
        }

        if (readabilityFixes.Count > 0)
        {
            var longParas = article.IntroParagraphs.Count(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 18);
            if (longParas > 0)
                checks.Add($"All intro paragraphs are 18 words or fewer ({longParas} are too long)");
            checks.Add("No paragraph exceeds 3 sentences");
        }

        foreach (var section in article.Sections)
        {
            if (!sectionsToRewrite.Contains(section.Heading, StringComparer.OrdinalIgnoreCase))
                checks.Add($"Section '{section.Heading}' is still present and unchanged");
        }

        return checks;
    }

    private static string BuildAdditionalInstructions(
        List<string> requiredFixes,
        List<string> sectionsToPreserve,
        List<string> sectionsToRewrite,
        decimal maxScope)
    {
        var instructions = new List<string>();

        if (sectionsToPreserve.Count > 0)
            instructions.Add($"KEEP these sections as-is (they pass QA): {string.Join(", ", sectionsToPreserve)}");

        if (sectionsToRewrite.Count > 0)
            instructions.Add($"REWRITE or ADD these sections: {string.Join(", ", sectionsToRewrite.Distinct())}");

        instructions.Add($"Maximum allowed rewrite scope: {maxScope}% of article");
        instructions.Add("Do NOT discard working content. Only change what is flagged.");

        return string.Join(". ", instructions);
    }

    private static HashSet<string> ExtractAlignmentTerms(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "from", "that", "this", "your", "into", "about", "what", "when", "where", "which", "have", "will", "guide", "best", "how"
        };

        return System.Text.RegularExpressions.Regex.Matches(text ?? string.Empty, "[a-zA-Z][a-zA-Z0-9-]+")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(x => x.Length >= 4 && !stopWords.Contains(x))
            .Take(12)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string[] BuildForbiddenPatterns(List<string> hardRuleFailures)
    {
        var patterns = new List<string>
        {
            "this section should",
            "the article should",
            "this guide will",
            "generic advice without a next step",
            "abstract filler",
            "repeated template language"
        };

        if (hardRuleFailures.Any(f => f.Contains("keyword", StringComparison.OrdinalIgnoreCase)))
            patterns.Add("exact keyword repetition / keyword stuffing");

        if (hardRuleFailures.Any(f => f.Contains("worked example", StringComparison.OrdinalIgnoreCase)))
            patterns.Add("sections with no worked example where one is required");

        if (hardRuleFailures.Any(f => f.Contains("step-by-step", StringComparison.OrdinalIgnoreCase) || f.Contains("action plan", StringComparison.OrdinalIgnoreCase)))
            patterns.Add("sections that explain ideas but give no next step");

        return patterns.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static string NormalizeHeading(string heading) =>
        System.Text.RegularExpressions.Regex.Replace(heading.Replace('\u2019', '\'').Replace('\u2018', '\''), @"\s+", " ").Trim();

    private static bool IsFaqHeading(string heading) =>
        System.Text.RegularExpressions.Regex.IsMatch(NormalizeHeading(heading), @"\b(?:FAQ|Frequently Asked Questions)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool IsLearnHeading(string heading) =>
        NormalizeHeading(heading).Contains("What You'll Learn", StringComparison.OrdinalIgnoreCase);

    // A named example needs a numerical calculation in its prose, not just a heading,
    // generic advice, formula labels, or numbers appearing in a source URL.
    // This recognizes structure; it does not verify arithmetic or factual claims.
    private static bool HasNamedCalculatedExample(IReadOnlyCollection<GeneratedSection> sections)
    {
        foreach (var section in sections)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(NormalizeHeading(section.Heading), @"\b(?:worked|hypothetical) example\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                continue;
            var prose = System.Text.RegularExpressions.Regex.Replace(string.Join("\n", section.Paragraphs), @"https?://[^\s)]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (System.Text.RegularExpressions.Regex.IsMatch(prose, @"(?<!\w)[-\u2212]?\$?\d[\d,]*(?:\.\d+)?(?:\s*[+\-\u2212*/\u00d7\u00f7]\s*[-\u2212]?\$?\d[\d,]*(?:\.\d+)?)+\s*=\s*[-\u2212]?\$?\d[\d,]*(?:\.\d+)?"))
                return true;
        }
        return false;
    }

    /// <summary>Recognizes a question followed by substantive non-question text in the same or a following line.</summary>
    /// <param name="text">FAQ paragraphs joined with line breaks.</param>
    /// <returns>True when at least one question has a following answer fragment longer than ten characters.</returns>
    private static bool HasQuestionAndAnswer(string text)
    {
        var questionSeen = false;
        foreach (var line in text.Split('\n'))
        {
            var questionIndex = line.LastIndexOf('?');
            if (questionIndex >= 0)
            {
                questionSeen = true;
                if (line[(questionIndex + 1)..].Trim().Length > 10)
                    return true;
            }
            else if (questionSeen && line.Trim().Length > 10)
                return true;
        }
        return false;
    }
}
