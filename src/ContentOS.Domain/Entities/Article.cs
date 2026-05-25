using System;
using System.Collections.Generic;

namespace ContentOS.Domain.Entities;

public class Article
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublished { get; set; }
    public string Status { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<ArticleVersion> Versions { get; set; } = new List<ArticleVersion>();
    public virtual ICollection<PublishRecord> PublishHistory { get; set; } = new List<PublishRecord>();
    public virtual ICollection<WorkflowRun> WorkflowRuns { get; set; } = new List<WorkflowRun>();
    public virtual ICollection<ArticleRevision> Revisions { get; set; } = new List<ArticleRevision>();
    public virtual ICollection<ArticleChatThread> ChatThreads { get; set; } = new List<ArticleChatThread>();
}