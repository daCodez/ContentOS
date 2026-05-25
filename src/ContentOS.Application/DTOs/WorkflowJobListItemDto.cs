namespace ContentOS.Application.DTOs;

public class WorkflowJobListItemDto
{
    public Guid WorkflowJobId { get; set; }
    public Guid ContentIdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; }
}
