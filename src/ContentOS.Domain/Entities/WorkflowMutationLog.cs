namespace ContentOS.Domain.Entities;

public class WorkflowMutationLog
{
    public Guid Id { get; set; }
    public Guid ContentWorkflowJobId { get; set; }
    public Guid? ContentWorkflowTaskId { get; set; }
    public string MutationType { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string OldValuesJson { get; set; } = "{}";
    public string NewValuesJson { get; set; } = "{}";
    public string ActorIdentity { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
