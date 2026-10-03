using ContentOS.Infrastructure.Agents;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ContentOS.Infrastructure.Writing;

public sealed record EditorialRevisionRequest(WorkflowArticleDraft Article, string Tone, string Instructions, string InputHash);
public sealed record EditorialRevisionResult(GeneratedLongformArticle Article, string InputHash, string OutputHash)
{
    public bool RequiresPostEditFactSourceLinkReview => true;
    public bool PublishReady => false;
    public string VerificationLimitations => "Mechanical preservation checks do not prove factual accuracy, source relevance, reading grade or link reachability.";
}

/// <summary>Performs actual structured editing, preserving constrained facts and structure before later review.</summary>
public sealed class EditorialRevisionService(IWorkflowArticleWriter writer)
{
    public async Task<EditorialRevisionResult> ReviseAsync(GeneratedLongformArticle article, string tone, string instructions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (article.IsSynthetic || article.Sections.Count == 0 || article.IntroParagraphs.Count == 0)
            throw new InvalidOperationException("Editorial revision requires an actual structured draft.");
        if (string.IsNullOrWhiteSpace(tone)) throw new InvalidOperationException("Editorial revision requires a frozen tone instruction.");
        var original = ToDraft(article);
        var inputHash = Hash(original);
        var response = await writer.ReviseArticleAsync(new(original, tone, instructions, inputHash), cancellationToken)
            ?? throw new InvalidOperationException("Editorial revision unavailable: configured writer returned no revised article.");
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IntroParagraphs is null || response.Sections is null || response.ConclusionParagraphs is null
            || response.Sections.Any(s => s is null || s.Paragraphs is null))
            throw new InvalidOperationException("Editorial revision returned incomplete structured content.");
        if (response.Title != original.Title || response.Slug != original.Slug
            || !response.Sections.Select(s => s.Heading).SequenceEqual(original.Sections.Select(s => s.Heading))
            || response.Sections.Count != original.Sections.Count)
            throw new InvalidOperationException("Editorial revision changed the frozen title, slug or section structure.");
        if (response.IntroParagraphs.Count != original.IntroParagraphs.Count || response.ConclusionParagraphs.Count != original.ConclusionParagraphs.Count
            || response.Sections.Where((s, i) => s.Paragraphs.Count != original.Sections[i].Paragraphs.Count).Any())
            throw new InvalidOperationException("Editorial revision removed or added structured paragraphs.");
        var originalText = Text(original); var revisedText = Text(response);
        if (!Tokens(originalText, @"\b\d[\d,]*(?:\.\d+)?\b").SequenceEqual(Tokens(revisedText, @"\b\d[\d,]*(?:\.\d+)?\b")))
            throw new InvalidOperationException("Editorial revision changed numeric claims; fact review is required before accepting this edit.");
        if (!Tokens(originalText, @"https?://[^\s\)\]>\""']+").SequenceEqual(Tokens(revisedText, @"https?://[^\s\)\]>\""']+")))
            throw new InvalidOperationException("Editorial revision changed source or link references.");
        if (!Lines(originalText, line => line.TrimStart().StartsWith('|')).SequenceEqual(Lines(revisedText, line => line.TrimStart().StartsWith('|'))))
            throw new InvalidOperationException("Editorial revision changed table content or format.");
        if (Regex.Matches(originalText, @"(?m)^\s*(?:[-*+] |\d+\. |\[[ xX]\] )").Count != Regex.Matches(revisedText, @"(?m)^\s*(?:[-*+] |\d+\. |\[[ xX]\] )").Count)
            throw new InvalidOperationException("Editorial revision changed bullet, checklist or numbered-list structure.");
        if (originalText == revisedText) throw new InvalidOperationException("Editorial revision returned unchanged prose; no edit was performed.");
        var body = Body(response); var count = Regex.Matches(revisedText, @"\S+").Count;
        var revised = article with
        {
            Summary = response.Summary, MetaDescription = response.MetaDescription,
            IntroParagraphs = response.IntroParagraphs.ToList(),
            Sections = response.Sections.Select((s, i) => new GeneratedSection(s.Heading, s.Paragraphs.ToList()) { SectionId = article.Sections[i].SectionId }).ToList(),
            ConclusionParagraphs = response.ConclusionParagraphs.ToList(), CallToAction = response.CallToAction,
            BodyText = body, FullText = revisedText, EstimatedWordCount = count, EstimatedReadTimeMinutes = Math.Max(1, (int)Math.Ceiling(count / 220m))
        };
        return new(revised, inputHash, Hash(ToDraft(revised)));
    }

    public static WorkflowArticleDraft ToDraft(GeneratedLongformArticle article) => new()
    {
        Title = article.Title, Slug = article.Slug, Summary = article.Summary, MetaDescription = article.MetaDescription,
        ContentType = "LongFormBlogArticle", TargetWordCountMin = article.TargetWordCountMin, TargetWordCountMax = article.TargetWordCountMax,
        IntroParagraphs = article.IntroParagraphs.ToList(), Sections = article.Sections.Select(s => new WorkflowArticleSectionDraft { Heading = s.Heading, Paragraphs = s.Paragraphs.ToList() }).ToList(),
        ConclusionParagraphs = article.ConclusionParagraphs.ToList(), CallToAction = article.CallToAction
    };
    public static string Hash(WorkflowArticleDraft article) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(article))));
    private static string Body(WorkflowArticleDraft draft) => string.Join("\n\n", draft.IntroParagraphs.Concat(draft.Sections.Select(s => "## " + s.Heading + "\n\n" + string.Join("\n\n", s.Paragraphs))).Concat(draft.ConclusionParagraphs).Append(draft.CallToAction));
    private static string Text(WorkflowArticleDraft draft) => draft.Title + "\n\n" + draft.Summary + "\n\n" + draft.MetaDescription + "\n\n" + Body(draft);
    private static IEnumerable<string> Tokens(string text, string pattern) => Regex.Matches(text, pattern).Select(m => m.Value).Order(StringComparer.Ordinal);
    private static IEnumerable<string> Lines(string text, Func<string,bool> predicate) => text.Split('\n').Where(predicate).Select(s => s.Trim());
}
