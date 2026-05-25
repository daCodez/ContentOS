namespace ContentOS.Application.DTOs;

public sealed class InjectWorkflowTaskRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string AssignedAgent { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string ScopeType { get; set; } = "Global";
    public string? ScopeId { get; set; }
    public Guid? InsertAfterTaskId { get; set; }
    public string? CreatedBy { get; set; }
    public string? Reason { get; set; }
    public string Source { get; set; } = "User";
    public string ActorType { get; set; } = "User";
    public string? CorrelationId { get; set; }
    public Guid? SupersedesTaskId { get; set; }
}

public sealed class WorkflowTaskMutationResultDto
{
    public Guid WorkflowJobId { get; set; }
    public Guid TaskId { get; set; }
    public int DisplayOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeId { get; set; }
}

public sealed class ReopenWorkflowRequestDto
{
    public string? Reason { get; set; }
    public string? CreatedBy { get; set; }
    public string Source { get; set; } = "User";
    public string ActorType { get; set; } = "User";
    public string? CorrelationId { get; set; }
}

public sealed class ReorderWorkflowTaskRequestDto
{
    public Guid TaskId { get; set; }
    public int NewDisplayOrder { get; set; }
    public string? Reason { get; set; }
    public string? CreatedBy { get; set; }
    public string Source { get; set; } = "User";
    public string ActorType { get; set; } = "User";
    public string? CorrelationId { get; set; }
}
