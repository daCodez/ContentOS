using System;
using System.Collections.Generic;

namespace ContentOS.Application.DTOs;

/// <summary>
/// Assembled article response built from completed workflow task outputs.
/// Provides a ready-to-render article with QA feedback, no frontend parsing required.
/// </summary>
public class ArticleAssemblyDto
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public double? QaScore { get; set; }
    public bool QaPassed { get; set; }
    public List<string> QaFeedback { get; set; } = new();
    public List<string> QaHardRuleFailures { get; set; } = new();
    public int EstimatedWordCount { get; set; }
    public int EstimatedReadTimeMinutes { get; set; }
    public bool PublishReady { get; set; }
    public Guid WorkflowJobId { get; set; }
    public Guid ContentIdeaId { get; set; }
    public string WorkflowStatus { get; set; } = string.Empty;
    public ArticleContentDto Content { get; set; } = new();
}

public class ArticleContentDto
{
    public List<string> Intro { get; set; } = new();
    public List<ArticleSectionDto> Sections { get; set; } = new();
    public List<string> Conclusion { get; set; } = new();
    public string Cta { get; set; } = string.Empty;
}

public class ArticleSectionDto
{
    public string Heading { get; set; } = string.Empty;
    public List<string> Paragraphs { get; set; } = new();
}