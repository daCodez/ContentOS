namespace ContentOS.Application.DTOs;

public sealed class WorkflowExportDto
{
    public Guid WorkflowJobId { get; set; }
    public string JobStatus { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public IReadOnlyCollection<WorkflowTaskExportDto> Tasks { get; set; } = Array.Empty<WorkflowTaskExportDto>();
}

public sealed class WorkflowTaskExportDto
{
    public Guid TaskId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string AssignedAgent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ScopeType { get; set; }
    public string? ScopeId { get; set; }
    public string? ResultArtifactId { get; set; }
    public string? ResolvedExecutionKey { get; set; }
    public string? ExecutionSummary { get; set; }
    public IReadOnlyCollection<string> Warnings { get; set; } = Array.Empty<string>();
    public Guid? PreviewArtifactId { get; set; }
    public IReadOnlyCollection<Guid> SupplementalArtifactIds { get; set; } = Array.Empty<Guid>();
}
