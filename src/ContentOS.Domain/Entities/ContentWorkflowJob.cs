using System;

namespace ContentOS.Domain.Entities;

public class ContentWorkflowJob
{
    public Guid Id { get; set; }
    public Guid ContentIdeaId { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public int WorkflowTemplateVersion { get; set; } = 1;
    public string Status { get; set; } = "Queued";
    public string CurrentStage { get; set; } = string.Empty;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string ErrorMessage { get; set; } = string.Empty;
}
