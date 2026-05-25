namespace ContentOS.Infrastructure.Agents;

public sealed record GeneratedLongformArticle(
    string Title,
    string Slug,
    string Summary,
    string MetaDescription,
    int TargetWordCountMin,
    int TargetWordCountMax,
    int EstimatedWordCount,
    int EstimatedReadTimeMinutes,
    List<string> IntroParagraphs,
    List<GeneratedSection> Sections,
    List<string> ConclusionParagraphs,
    string CallToAction,
    string BodyText,
    string FullText,
    bool IsSynthetic,
    bool MeetsMinimumQuality);

public sealed record GeneratedSection(string Heading, List<string> Paragraphs)
{
    public string SectionId { get; init; } = Heading.Length > 0 ? Heading.ToLowerInvariant().Replace(' ', '-').Trim() : Guid.NewGuid().ToString("n")[..8];
    public string BodyText => string.Join("\n\n", Paragraphs);
}
