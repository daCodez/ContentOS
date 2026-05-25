using System;

namespace ContentOS.Domain.Entities;

public class ArticleVersion
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
    
    public virtual Article Article { get; set; } = null!;
}