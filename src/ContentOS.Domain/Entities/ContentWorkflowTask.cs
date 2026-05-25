using System;
using ContentOS.Domain.Enums;

namespace ContentOS.Domain.Entities;

public class ContentWorkflowTask
{
    public Guid Id { get; set; }
    public Guid ContentWorkflowJobId { get; set; }
    public Guid? WorkflowTaskTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string AssignedAgent { get; set; } = string.Empty;
    public ContentOS.Domain.Enums.TaskStatus Status { get; set; } = ContentOS.Domain.Enums.TaskStatus.Pending;
    public TaskKind Kind { get; set; } = TaskKind.Template;
    public TargetScopeType ScopeType { get; set; } = TargetScopeType.Global;
    public string? ScopeId { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string InputDataJson { get; set; } = "{}";
    public string OutputDataJson { get; set; } = "{}";
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? InsertedAfterTaskId { get; set; }
    public string? ResultArtifactId { get; set; }
    public string? PatchPayload { get; set; }
    public int ExecutionAttemptCount { get; set; }
    public Guid? SupersedesTaskId { get; set; }
}
