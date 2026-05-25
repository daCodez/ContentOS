using System;

namespace ContentOS.Domain.Entities;

public class ContentResearchSource
{
    public Guid Id { get; set; }
    public Guid ContentIdeaId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
