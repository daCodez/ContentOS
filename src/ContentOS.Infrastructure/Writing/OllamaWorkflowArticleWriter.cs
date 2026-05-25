using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Agents;

namespace ContentOS.Infrastructure.Writing;

public sealed class OllamaWorkflowArticleWriter : IWorkflowArticleWriter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaWorkflowArticleWriter> _logger;
    private readonly IEditorialExemplarService _editorialExemplars;

    public OllamaWorkflowArticleWriter(HttpClient httpClient, ILogger<OllamaWorkflowArticleWriter> logger, IEditorialExemplarService editorialExemplars)
    {
        _httpClient = httpClient;
        _logger = logger;
        _editorialExemplars = editorialExemplars;
    }

    public async Task<WorkflowArticleDraft?> GenerateDraftAsync(
        string contentType,
        string title,
        string slug,
        string primaryKeyword,
        string summary,
        string searchIntent,
        string audiencePainPoint,
        string audienceGoal,
        string recommendedAngle,
        string whyNow,
        IReadOnlyCollection<string> secondaryKeywords,
        IReadOnlyCollection<string> sourceSummaries,
        int targetWordCountMin,
        int targetWordCountMax,
        TopicExpansionResult? topicExpansion = null,
        ArticleOutlineResult? outline = null,
        string? reworkDirectiveJson = null,
        CancellationToken cancellationToken = default)
    {
        const string model = "gemma4:31b-cloud";

        var exemplarContext = await _editorialExemplars.BuildContextAsync(contentType, primaryKeyword, summary, cancellationToken);

        var prompt = BuildPrompt(
            contentType,
            title,
            slug,
            primaryKeyword,
            summary,
            searchIntent,
            audiencePainPoint,
            audienceGoal,
            recommendedAngle,
            whyNow,
            secondaryKeywords,
            sourceSummaries,
            targetWordCountMin,
            targetWordCountMax,
            reworkDirectiveJson,
            exemplarContext,
            topicExpansion,
            outline);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/chat", new
            {
                model,
                stream = false,
                format = "json",
                messages = new object[]
                {
            new { role = "system", content = @"You are a professional blog writer. Your output must be the FINAL article content only. 

STRICT RULES:
1. NO meta-commentary. Do not describe what you are doing. Do not say 'I have rewritten this section' or 'This section addresses the pain point'.
2. NO instructional language. Do not write about the process of writing.
3. VOICE: Knowledgeable friend, direct, specific, zero corporate jargon. 
4. FORMAT: Return valid JSON only.

Example of CORRECT voice:
'You open your banking app and feel that knot. You're not alone — most people have no idea where their money goes. Here's a way to fix that.'

Example of INCORRECT voice (NEVER do this):
'This section should address the reader's pain point and reduce decision fatigue by narrowing the field.'

Keep sentences short. Use bullets/numbers for clarity. Break paragraphs at 2-3 sentences. Use exact primary keyword in title and intro only; vary it naturally everywhere else." },
                    new { role = "user", content = prompt }
                }
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Workflow writer call failed with status {StatusCode}: {Body}", response.StatusCode, failureBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                _logger.LogWarning("Workflow writer response did not include message.content.");
                return null;
            }

            var content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Workflow writer returned empty content.");
                return null;
            }

            var draft = JsonSerializer.Deserialize<WorkflowArticleDraft>(content, JsonOptions);
            return draft is null ? null : NormalizeDraft(draft, title, slug, summary, contentType, targetWordCountMin, targetWordCountMax);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workflow writer call failed.");
            return null;
        }
    }

    public async Task<IdeationResponse?> GenerateIdeationResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        const string model = "gemma4:31b-cloud";

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/chat", new
            {
                model,
                stream = false,
                format = "json",
                messages = new object[]
                {
                    new { role = "system", content = "You are a senior content strategist. Given raw research findings, generate distinct, high-quality article ideas. Return ONLY a JSON object matching the requested schema." },
                    new { role = "user", content = prompt }
                }
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ideation writer call failed with status {StatusCode}: {Body}", response.StatusCode, failureBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                _logger.LogWarning("Ideation writer response did not include message.content.");
                return null;
            }

            var content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return JsonSerializer.Deserialize<IdeationResponse>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ideation writer call failed.");
            return null;
        }
    }

    private static WorkflowArticleDraft NormalizeDraft(
        WorkflowArticleDraft draft,
        string title,
        string slug,
        string summary,
        string contentType,
        int targetWordCountMin,
        int targetWordCountMax)
    {
        return new WorkflowArticleDraft
        {
            Title = string.IsNullOrWhiteSpace(draft.Title) ? title : draft.Title.Trim(),
            Slug = string.IsNullOrWhiteSpace(draft.Slug) ? slug : draft.Slug.Trim(),
            Summary = string.IsNullOrWhiteSpace(draft.Summary) ? summary : draft.Summary.Trim(),
            MetaDescription = SafeTrim(draft.MetaDescription),
            ContentType = string.IsNullOrWhiteSpace(draft.ContentType) ? contentType : draft.ContentType.Trim(),
            TargetWordCountMin = draft.TargetWordCountMin <= 0 ? targetWordCountMin : draft.TargetWordCountMin,
            TargetWordCountMax = draft.TargetWordCountMax <= 0 ? targetWordCountMax : draft.TargetWordCountMax,
            IntroParagraphs = CleanParagraphs(draft.IntroParagraphs),
            Sections = (draft.Sections ?? new List<WorkflowArticleSectionDraft>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Heading))
                .Select(s => new WorkflowArticleSectionDraft
                {
                    Heading = s.Heading.Trim(),
                    Paragraphs = CleanParagraphs(s.Paragraphs)
                })
                .Where(s => s.Paragraphs.Count > 0)
                .ToList(),
            ConclusionParagraphs = CleanParagraphs(draft.ConclusionParagraphs),
            CallToAction = SafeTrim(draft.CallToAction),
            EstimatedWordCount = Math.Max(0, draft.EstimatedWordCount),
            EstimatedReadTimeMinutes = Math.Max(0, draft.EstimatedReadTimeMinutes)
        };
    }

    private static List<string> CleanParagraphs(List<string>? paragraphs)
        => (paragraphs ?? new List<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

    private static string SafeTrim(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildPrompt(
        string contentType,
        string title,
        string slug,
        string primaryKeyword,
        string summary,
        string searchIntent,
        string audiencePainPoint,
        string audienceGoal,
        string recommendedAngle,
        string whyNow,
        IReadOnlyCollection<string> secondaryKeywords,
        IReadOnlyCollection<string> sourceSummaries,
        int targetWordCountMin,
        int targetWordCountMax,
        string? reworkDirectiveJson = null,
        EditorialExemplarContext? exemplarContext = null,
        TopicExpansionResult? topicExpansion = null,
        ArticleOutlineResult? outline = null)
    {
        var builder = new StringBuilder();

        // Parse rework directive if present
        ContentOS.Domain.Qa.QaReworkDirective? rework = null;
        if (!string.IsNullOrWhiteSpace(reworkDirectiveJson))
        {
            try
            {
                // First try to extract from wrapper object
                var directiveJson = ExtractReworkDirectiveJson(reworkDirectiveJson) ?? reworkDirectiveJson;
                rework = System.Text.Json.JsonSerializer.Deserialize<ContentOS.Domain.Qa.QaReworkDirective>(directiveJson, JsonOptions);
            }
            catch { /* ignore parse failures */ }
        }

        if (rework != null)
        {
            builder.AppendLine("### REWORK DATA MANIFEST");
            builder.AppendLine($"Attempt: {rework.AttemptNumber} | Strategy: {rework.Summary} | Max Scope: {rework.MaxRewriteScopePercent}%");
            
            if (!string.IsNullOrWhiteSpace(rework.BeforeAfterSummary))
            {
                builder.AppendLine($"Goal: {rework.BeforeAfterSummary}");
            }

            var critical = rework.FixItems.Where(f => f.Priority == ContentOS.Domain.Qa.QaFixPriority.Critical).ToList();
            var major = rework.FixItems.Where(f => f.Priority == ContentOS.Domain.Qa.QaFixPriority.Major).ToList();
            var minor = rework.FixItems.Where(f => f.Priority == ContentOS.Domain.Qa.QaFixPriority.Minor).ToList();

            if (critical.Count > 0)
            {
                builder.AppendLine("CRITICAL FIXES:");
                foreach (var fix in critical) builder.AppendLine($"- {fix.Section}: {fix.Description}");
            }
            if (major.Count > 0)
            {
                builder.AppendLine("MAJOR FIXES:");
                foreach (var fix in major) builder.AppendLine($"- {fix.Section}: {fix.Description}");
            }
            if (minor.Count > 0)
            {
                builder.AppendLine("MINOR FIXES:");
                foreach (var fix in minor) builder.AppendLine($"- {fix.Section}: {fix.Description}");
            }

            if (rework.HardRuleFailures.Count > 0)
            {
                builder.AppendLine("HARD RULE FAILURES:");
                foreach (var failure in rework.HardRuleFailures) builder.AppendLine($"- {failure}");
            }

            if (rework.RequiredFixes.Count > 0)
            {
                builder.AppendLine("FAILED RUBRIC ITEMS:");
                foreach (var fix in rework.RequiredFixes) builder.AppendLine($"- {fix}");
            }

            if (rework.SectionsToPreserve.Count > 0)
                builder.AppendLine($"PRESERVE EXACTLY: {string.Join(", ", rework.SectionsToPreserve)}");

            if (rework.SectionsToRewrite.Count > 0)
                builder.AppendLine($"TARGET SECTIONS ONLY: {string.Join(", ", rework.SectionsToRewrite)}");

            if (rework.ForbiddenPatterns.Count > 0)
            {
                builder.AppendLine("FORBIDDEN PATTERNS:");
                foreach (var pattern in rework.ForbiddenPatterns) builder.AppendLine($"- {pattern}");
            }

            if (rework.ExemplarPatterns.Count > 0)
            {
                builder.AppendLine("TARGET EXEMPLAR PATTERNS:");
                foreach (var pattern in rework.ExemplarPatterns) builder.AppendLine($"- {pattern}");
            }

            builder.AppendLine("\n### REWRITE CONSTRAINTS");
            builder.AppendLine("1. Preserve sections exactly as provided.");
            builder.AppendLine("2. Rewrite only target sections and only for failed rubric items.");
            builder.AppendLine("3. Do not introduce forbidden patterns.");
            builder.AppendLine("4. Output only the final article JSON.");
            builder.AppendLine();
        }

                        builder.AppendLine("### TOPIC MAP & COVERAGE");
        if (topicExpansion != null)
        {
            builder.AppendLine($"Core Intent: {topicExpansion.CoreIntent}");
            builder.AppendLine("Must-Answer Questions:");
            foreach (var q in topicExpansion.MustAnswerQuestions) builder.AppendLine($"- {q}");
            builder.AppendLine("Subtopic Coverage:");
            foreach (var s in topicExpansion.Subtopics) builder.AppendLine($"- {s}");
            builder.AppendLine("Required Evidence/Examples:");
            foreach (var e in topicExpansion.WorkedExampleIdeas) builder.AppendLine($"- {e}");
        }
        else
        {
            builder.AppendLine("Follow the standard editorial rubric for coverage.");
        }

        var normalizedPrimaryKeyword = NormalizeKeyword(primaryKeyword, title);
        var normalizedSecondaryKeywords = secondaryKeywords
            .Select(k => NormalizeKeyword(k, title))
            .Where(k => !string.IsNullOrWhiteSpace(k) && !string.Equals(k, normalizedPrimaryKeyword, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var supportingPhrases = secondaryKeywords
            .Select(k => NormalizePhrase(k))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        builder.AppendLine("\n### TOPIC MAP & COVERAGE");
        if (topicExpansion != null)
        {
            builder.AppendLine($"Core Intent: {topicExpansion.CoreIntent}");
            builder.AppendLine("Must-Answer Questions:");
            foreach (var q in topicExpansion.MustAnswerQuestions) builder.AppendLine($"- {q}");
            builder.AppendLine("Subtopic Coverage:");
            foreach (var s in topicExpansion.Subtopics) builder.AppendLine($"- {s}");
            builder.AppendLine("Required Evidence/Examples:");
            foreach (var e in topicExpansion.WorkedExampleIdeas) builder.AppendLine($"- {e}");
        }
        else
        {
            builder.AppendLine("Follow the standard editorial rubric for coverage.");
        }

        builder.AppendLine("\n### STRUCTURED OUTLINE");
        if (outline != null)
        {
            foreach (var sec in outline.Sections)
                builder.AppendLine($"- {sec.Id}: {sec.Title}");
        }
        else
        {
            builder.AppendLine("Use standard longform layout: Intro, Quick Answer, Worked Example, Steps, Tips, Mistakes, FAQ, CTA.");
        }

        builder.AppendLine("\n### CONTENT DATA");
        builder.AppendLine($"Title: {title}");
        builder.AppendLine($"Slug: {slug}");
        builder.AppendLine($"Primary Keyword: {normalizedPrimaryKeyword}");
        builder.AppendLine($"Summary: {summary}");
        builder.AppendLine($"Search Intent: {searchIntent}");
        builder.AppendLine($"Audience Pain Point: {audiencePainPoint}");
        builder.AppendLine($"Audience Goal: {audienceGoal}");
        builder.AppendLine($"Recommended Angle: {recommendedAngle}");
        builder.AppendLine($"Why Now: {whyNow}");
        builder.AppendLine($"Secondary Keywords: {string.Join(", ", normalizedSecondaryKeywords)}");
        builder.AppendLine($"Supporting Phrases: {string.Join(", ", supportingPhrases)}");
        builder.AppendLine($"Evidence / source summaries: {string.Join(" | ", sourceSummaries)}");
        builder.AppendLine($"Target Length: {targetWordCountMin} to {targetWordCountMax} words.");
        builder.AppendLine($"Content Type: {contentType}");
        builder.AppendLine("Voice: grade 6-8 reading level. Direct. No jargon. No filler.");
        builder.AppendLine("Keyword model: one natural primary keyword, 3 to 5 secondary keywords, and supporting phrases for coverage.");
        builder.AppendLine("Keyword rule: if a phrase sounds unnatural or too long for a real search, do not force it into the draft.");
        builder.AppendLine("Use the primary keyword in the title, once in the intro, and only 1 to 2 times in the body.");
        builder.AppendLine("Use secondary keywords and supporting phrases naturally. Do not force exact-match repetition.");
        builder.AppendLine("EDITORIAL TARGETS:");
        builder.AppendLine("- Strong hook fast.");
        builder.AppendLine("- Answer early.");
        builder.AppendLine("- Use real numbers.");
        builder.AppendLine("- Include a worked example.");
        builder.AppendLine("- Include clear step-by-step actions.");
        builder.AppendLine("- Keep paragraphs short.");
        builder.AppendLine("- Every major section must help the reader act.");
        builder.AppendLine("- Use external authority facts only when grounded in provided source summaries. If no support is present, phrase as practical guidance, not factual claim.");
        builder.AppendLine("BANNED PATTERNS:");
        builder.AppendLine("- Meta commentary.");
        builder.AppendLine("- Abstract filler.");
        builder.AppendLine("- Repeated template language.");
        builder.AppendLine("- Generic advice without a next step.");
        builder.AppendLine("- Section text that explains ideas but gives no action.");
        if (exemplarContext is not null && exemplarContext.SharedPatterns.Count > 0)
        {
            builder.AppendLine("EXEMPLAR PATTERNS:");
            foreach (var pattern in exemplarContext.SharedPatterns.Take(5))
                builder.AppendLine($"- {pattern}");
        }
        if (exemplarContext is not null && exemplarContext.Examples.Count > 0)
        {
            builder.AppendLine("EXEMPLAR REFERENCES:");
            foreach (var example in exemplarContext.Examples.Take(2))
            {
                builder.AppendLine($"- {example.Title}");
                foreach (var pattern in example.PatternSummary.Take(3))
                    builder.AppendLine($"  - {pattern}");
            }
        }
        builder.AppendLine("Return valid JSON with exactly these top-level properties: title, slug, summary, metaDescription, contentType, targetWordCountMin, targetWordCountMax, estimatedWordCount, estimatedReadTimeMinutes, introParagraphs, sections, conclusionParagraphs, callToAction.");
        builder.AppendLine("sections must be an array of objects with: heading, paragraphs.");
        builder.AppendLine("Each paragraph string may contain short bullet or numbered list lines when useful.");
        return builder.ToString();
    }

    private static string NormalizeKeyword(string? phrase, string fallback)
    {
        var normalized = NormalizePhrase(string.IsNullOrWhiteSpace(phrase) ? fallback : phrase);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 5)
        {
            normalized = string.Join(' ', words.Take(5));
        }
        return normalized;
    }

    private static string NormalizePhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;
        var normalized = phrase.Trim();
        normalized = normalized.StartsWith("how to ", StringComparison.OrdinalIgnoreCase)
            ? normalized[7..].Trim()
            : normalized;
        normalized = normalized.Replace(':', ' ');
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record ReworkDirectiveData(
        bool AutomaticRewrite,
        int RewriteAttempt,
        string? RequestedBy,
        string? Reason,
        string? ReworkStrategy,
        string[]? TargetedFixes,
        string[]? MissingSections,
        string[]? WeakDimensions,
        string[]? KeywordGaps,
        string[]? FormattingFixes);

    private static string? ExtractReworkDirectiveJson(string? inputDataJson)
    {
        if (string.IsNullOrWhiteSpace(inputDataJson) || inputDataJson == "{}") return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(inputDataJson);
            if (doc.RootElement.TryGetProperty("reworkDirective", out var element))
            {
                return element.GetRawText();
            }
        }
        catch { /* ignore */ }

        // Legacy format: if reworkStrategy is present at top level, the whole thing is the directive
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(inputDataJson);
            if (doc.RootElement.TryGetProperty("reworkStrategy", out _))
            {
                return inputDataJson;
            }
        }
        catch { /* ignore */ }

        return null;
    }
}
