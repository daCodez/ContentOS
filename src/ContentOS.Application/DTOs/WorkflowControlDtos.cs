namespace ContentOS.Application.DTOs;

public sealed class WorkflowTaskControlRequestDto
{
    public string? Reason { get; set; }
    public string? CreatedBy { get; set; }
    public string Source { get; set; } = "User";
    public string ActorType { get; set; } = "User";
    public string? CorrelationId { get; set; }
}

public sealed class WorkflowControlResultDto
{
    public Guid WorkflowJobId { get; set; }
    public Guid? TaskId { get; set; }
    public string JobStatus { get; set; } = string.Empty;
    public string? TaskStatus { get; set; }
    public string Message { get; set; } = string.Empty;
}
