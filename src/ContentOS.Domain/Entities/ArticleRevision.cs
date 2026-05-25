using System;

namespace ContentOS.Domain.Entities;

public class ArticleRevision
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? RequestId { get; set; }
    public int RevisionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public string DiffSummary { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
