using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Application.DTOs;
using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Articles;

public interface IArticleEditRunner
{
    string AgentName { get; }
    Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default);
}

public sealed record ArticleEditRunnerContext(
    Guid ArticleId,
    string Intent,
    string Scope,
    string Message,
    string TargetLabel,
    string OriginalContent,
    string ArticleTitle,
    string ArticleSummary,
    string ArticleMetaDescription,
    string ArticleCallToAction,
    ArticleFieldLocksDto FieldLocks);

public sealed record ArticleEditRunnerResult(
    string AgentName,
    string Operation,
    string ProposedContent,
    string? ProposedTitle,
    string? ProposedSummary,
    string? ProposedMetaDescription,
    string? ProposedCallToAction,
    string Rationale,
    IReadOnlyList<string> Warnings,
    string ModelNotes);

public sealed class WriterArticleEditRunner : IArticleEditRunner
{
    private readonly IWorkflowArticleWriter _writer;
    private readonly ILogger<WriterArticleEditRunner> _logger;

    public WriterArticleEditRunner(IWorkflowArticleWriter writer, ILogger<WriterArticleEditRunner> logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public string AgentName => "WriterAgent";

    public async Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt("You are WriterAgent. Rewrite only the requested target.", context);
        var response = await _writer.GenerateIdeationResponseAsync(prompt, cancellationToken);
        var rewritten = ExtractText(response) ?? RewriteFallback(context.OriginalContent);
        _logger.LogInformation("WriterArticleEditRunner used fallback={Fallback}", response is null);

        return new ArticleEditRunnerResult(
            AgentName,
            "rewrite",
            rewritten,
            context.FieldLocks.TitleLocked ? null : (context.Message.Contains("title", StringComparison.OrdinalIgnoreCase) ? ImproveTitle(context.ArticleTitle) : null),
            context.FieldLocks.SummaryLocked ? null : (context.Message.Contains("summary", StringComparison.OrdinalIgnoreCase) ? Summarize(rewritten, context.ArticleSummary) : null),
            null,
            null,
            "WriterAgent produced a targeted rewrite focused on clarity and structure.",
            Array.Empty<string>(),
            response is null ? "Fallback rewrite used." : "LLM-guided rewrite used.");
    }

    private static string RewriteFallback(string html)
    {
        var updated = Regex.Replace(html, "\bHowever,\b", "But", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bIn order to\b", "To", RegexOptions.IgnoreCase);
        return updated;
    }

    private static string BuildPrompt(string systemLine, ArticleEditRunnerContext context)
        => $"{systemLine}\nReturn JSON like {{\"ideas\":[{{\"title\":\"<rewritten html only>\"}}]}}.\nScope: {context.Scope}\nTarget: {context.TargetLabel}\nRequest: {context.Message}\nOriginal HTML:\n{context.OriginalContent}";

    private static string? ExtractText(ContentOS.Infrastructure.Ideation.IdeationResponse? response)
        => response?.Ideas?.FirstOrDefault()?.Title;

    private static string ImproveTitle(string title)
        => string.IsNullOrWhiteSpace(title) || title.Contains(':', StringComparison.Ordinal) ? title : $"{title}: A Practical Guide";

    private static string Summarize(string html, string fallback)
    {
        var plain = Regex.Replace(System.Net.WebUtility.HtmlDecode(html ?? string.Empty), "<[^>]+>", " ").Trim();
        return plain.Length < 40 ? fallback : (plain.Length > 180 ? plain[..180].TrimEnd() + "..." : plain);
    }
}

public sealed class HumanizerArticleEditRunner : IArticleEditRunner
{
    public string AgentName => "HumanizerAgent";

    public Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default)
    {
        var updated = context.OriginalContent;
        updated = Regex.Replace(updated, "\bIt is important to note that\b", "A useful thing to remember is", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bAdditionally,\b", "Also,", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bIn conclusion,?\b", "To wrap up,", RegexOptions.IgnoreCase);

        return Task.FromResult(new ArticleEditRunnerResult(
            AgentName,
            "rewrite",
            updated,
            null,
            null,
            null,
            null,
            "HumanizerAgent softened robotic phrasing while preserving structure.",
            Array.Empty<string>(),
            "Rule-based humanizer pass."));
    }
}

public sealed class SeoArticleEditRunner : IArticleEditRunner
{
    public string AgentName => "SeoOptimizerAgent";

    public Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default)
    {
        var updated = context.OriginalContent;
        if (!Regex.IsMatch(updated, "<strong>Key takeaway:</strong>", RegexOptions.IgnoreCase))
        {
            updated = $"<p><strong>Key takeaway:</strong> {System.Net.WebUtility.HtmlEncode(context.ArticleTitle)} works best when the steps are specific and practical.</p>{updated}";
        }

        var meta = context.FieldLocks.MetaDescriptionLocked ? null : BuildMetaDescription(context.ArticleTitle, context.ArticleSummary);

        return Task.FromResult(new ArticleEditRunnerResult(
            AgentName,
            "rewrite",
            updated,
            null,
            null,
            meta,
            null,
            "SeoOptimizerAgent strengthened on-page clarity and meta coverage without broad rewriting.",
            Array.Empty<string>(),
            "SEO enrichment pass."));
    }

    private static string BuildMetaDescription(string title, string summary)
    {
        var seed = string.IsNullOrWhiteSpace(summary) ? title : summary;
        var plain = Regex.Replace(System.Net.WebUtility.HtmlDecode(seed ?? string.Empty), "<[^>]+>", " ").Trim();
        return plain.Length > 150 ? plain[..150].TrimEnd() : plain;
    }
}

public sealed class MonetizationArticleEditRunner : IArticleEditRunner
{
    public string AgentName => "MonetizationAgent";

    public Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default)
    {
        var cta = context.FieldLocks.CallToActionLocked
            ? null
            : (string.IsNullOrWhiteSpace(context.ArticleCallToAction)
                ? "Ready to put this into practice? Start with one small change today and build from there."
                : context.ArticleCallToAction);

        return Task.FromResult(new ArticleEditRunnerResult(
            AgentName,
            "update-field",
            context.OriginalContent,
            null,
            null,
            null,
            cta,
            "MonetizationAgent proposed a clearer next-step CTA without changing article structure.",
            Array.Empty<string>(),
            "CTA-focused pass."));
    }
}

public sealed class QaReviewArticleEditRunner : IArticleEditRunner
{
    public string AgentName => "QaAgent";

    public Task<ArticleEditRunnerResult> RunAsync(ArticleEditRunnerContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ArticleEditRunnerResult(
            AgentName,
            "review",
            context.OriginalContent,
            null,
            null,
            null,
            null,
            "QaAgent marked this as research-sensitive and suggested a review-first flow.",
            new[] { "Manual review recommended before apply." },
            "Review-only QA pass."));
    }
}
