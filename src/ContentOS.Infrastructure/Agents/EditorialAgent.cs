namespace ContentOS.Infrastructure.Agents;

public interface IEditorialAgent
{
    Task<EditorialPassResult> HumanizeAndImproveReadabilityAsync(GeneratedLongformArticle article, CancellationToken cancellationToken = default);
    Task<HeadlinePackResult> ImproveHeadlineAndHookAsync(GeneratedLongformArticle article, string primaryKeyword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Humanizer constraints (enforced at pipeline level):
/// - NO adding sections
/// - NO removing bullets
/// - NO changing structure (headings, section order, table format)
/// - ONLY: smoother wording, better flow, natural phrasing
/// These constraints are documented here and enforced by the pipeline orchestration.
/// </summary>
public sealed class EditorialAgent : IEditorialAgent
{
    public Task<EditorialPassResult> HumanizeAndImproveReadabilityAsync(GeneratedLongformArticle article, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasShortHook = article.IntroParagraphs.Take(3).All(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 18);

        return Task.FromResult(new EditorialPassResult(
            "Readability",
            new[]
            {
                "Assessment only: article wording was retained; no editorial revision was performed",
                "Reading level and engaging prose require an actual editorial revision and review",
                hasShortHook ? "Hook lines are short and punchy" : "Hook lines still need tightening"
            },
            article.EstimatedWordCount,
            article.Sections.Count,
            !article.IsSynthetic && article.EstimatedWordCount >= 1200 && hasShortHook));
    }

    /// <summary>Returns the existing article headline and opening instead of fabricating keyword-based promises.</summary>
    /// <param name="article">The actual draft whose wording is retained.</param>
    /// <param name="primaryKeyword">The topic phrase; it is not inserted into a generic headline template.</param>
    /// <param name="cancellationToken">Cancellation for the workflow.</param>
    /// <returns>A pack grounded in draft text, with no invented alternatives or statistical hooks.</returns>
    public Task<HeadlinePackResult> ImproveHeadlineAndHookAsync(GeneratedLongformArticle article, string primaryKeyword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primaryHeadline = article.Title.Trim();
        var hookLines = article.IntroParagraphs.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3).ToArray();

        return Task.FromResult(new HeadlinePackResult(
            primaryHeadline,
            Array.Empty<string>(),
            primaryHeadline,
            hookLines,
            !string.IsNullOrWhiteSpace(primaryHeadline) && hookLines.Length >= 2));
    }
}
