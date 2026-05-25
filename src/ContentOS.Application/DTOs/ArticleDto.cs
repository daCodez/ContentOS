using System;

namespace ContentOS.Application.DTOs;

public class ArticleDto
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
}