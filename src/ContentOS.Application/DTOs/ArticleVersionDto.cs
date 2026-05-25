using System;

namespace ContentOS.Application.DTOs;

public class ArticleVersionDto
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
}