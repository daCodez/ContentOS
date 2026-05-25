using System;

namespace ContentOS.Domain.Entities;

public class ContentArtifact
{
    public Guid Id { get; set; }
    public Guid ContentWorkflowJobId { get; set; }
    public Guid? ContentWorkflowTaskId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StorageType { get; set; } = "Database";
    public string ContentJson { get; set; } = "{}";
    public string ContentText { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public int VersionNumber { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByAgent { get; set; } = string.Empty;
}
