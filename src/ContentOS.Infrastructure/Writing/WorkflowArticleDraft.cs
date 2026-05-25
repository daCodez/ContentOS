namespace ContentOS.Infrastructure.Writing;

public sealed class WorkflowArticleDraft
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int TargetWordCountMin { get; set; }
    public int TargetWordCountMax { get; set; }
    public List<string> IntroParagraphs { get; set; } = new();
    public List<WorkflowArticleSectionDraft> Sections { get; set; } = new();
    public List<string> ConclusionParagraphs { get; set; } = new();
    public string CallToAction { get; set; } = string.Empty;
    public int EstimatedWordCount { get; set; }
    public int EstimatedReadTimeMinutes { get; set; }
}

public sealed class WorkflowArticleSectionDraft
{
    public string Heading { get; set; } = string.Empty;
    public List<string> Paragraphs { get; set; } = new();
}