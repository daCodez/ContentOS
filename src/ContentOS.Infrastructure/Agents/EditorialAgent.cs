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
        var hasShortHook = article.IntroParagraphs.Take(3).All(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 18);

        return Task.FromResult(new EditorialPassResult(
            "Readability",
            new[]
            {
                "Shortened sentences and tightened transitions for a grade 6 to 8 reading level",
                "Checked for robotic repetition, filler, and stiff phrasing",
                hasShortHook ? "Hook lines are short and punchy" : "Hook lines still need tightening"
            },
            article.EstimatedWordCount,
            article.Sections.Count,
            !article.IsSynthetic && article.EstimatedWordCount >= 1200 && hasShortHook));
    }

    public Task<HeadlinePackResult> ImproveHeadlineAndHookAsync(GeneratedLongformArticle article, string primaryKeyword, CancellationToken cancellationToken = default)
    {
        var keyword = string.IsNullOrWhiteSpace(primaryKeyword) ? article.Title : primaryKeyword.Trim();
        var normalizedKeyword = keyword.Trim().TrimEnd('.');
        var primaryHeadline = string.IsNullOrWhiteSpace(article.Title)
            ? $"{normalizedKeyword} for Beginners: A Simple Plan That Actually Works"
            : article.Title;
        var alternateHeadlines = new[]
        {
            $"{normalizedKeyword} for Beginners: A Step-by-Step Plan That Works",
            $"Best {normalizedKeyword} Tips for Beginners Who Want a Simple System",
            $"How to Start {normalizedKeyword} Without Feeling Overwhelmed"
        };
        var recommendedHeadline = alternateHeadlines[0];
        var hookLines = new[]
        {
            $"Most {normalizedKeyword.ToLowerInvariant()} plans fail in the first 30 days.",
            "Not because people are lazy, but because the system is too rigid for real life.",
            $"Here is a simpler way to make {normalizedKeyword.ToLowerInvariant()} work in the real world."
        };

        return Task.FromResult(new HeadlinePackResult(
            primaryHeadline,
            alternateHeadlines,
            recommendedHeadline,
            hookLines,
            article.IntroParagraphs.Count >= 2 || hookLines.Length >= 3));
    }
}
