using ContentOS.Domain.Enums;

namespace ContentOS.Domain.Entities;

public class WorkflowDefinitionRun
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionFamilyId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinitionType WorkflowType { get; set; }
    public int Version { get; set; }
    public WorkflowDefinitionRunStatus Status { get; set; } = WorkflowDefinitionRunStatus.Pending;
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string InputSnapshotJson { get; set; } = "{}";
    public Guid? IdeaRecordId { get; set; }
}

public class WorkflowActionRun
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionRunId { get; set; }
    public Guid WorkflowActionDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public WorkflowDefinitionRunStatus Status { get; set; } = WorkflowDefinitionRunStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetry { get; set; } = 3;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InputSnapshotJson { get; set; }
    public string? OutputSnapshotJson { get; set; }
}

public class WorkflowStepRun
{
    public Guid Id { get; set; }
    public Guid WorkflowActionRunId { get; set; }
    public Guid WorkflowStepDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public WorkflowDefinitionRunStatus Status { get; set; } = WorkflowDefinitionRunStatus.Pending;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InputSnapshotJson { get; set; }
    public string? OutputSnapshotJson { get; set; }
}

public class IdeaRecord
{
    public Guid Id { get; set; }
    public Guid? SourceWorkflowRunId { get; set; }
    public Guid? SourceWorkflowDefinitionId { get; set; }
    public int WorkflowVersion { get; set; }
    public Guid? SiteId { get; set; }
    public string IdeaTitle { get; set; } = string.Empty;
    public string ReaderProblem { get; set; } = string.Empty;
    public string AudienceType { get; set; } = string.Empty;
    public string SearchIntent { get; set; } = string.Empty;
    public string EmotionalTrigger { get; set; } = string.Empty;
    public string UniquenessAngle { get; set; } = string.Empty;
    public decimal MonetizationFit { get; set; }
    public decimal SeoPotential { get; set; }
    public decimal Difficulty { get; set; }
    public decimal PriorityScore { get; set; }
    public IdeaRecordStatus Status { get; set; } = IdeaRecordStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? RejectedUtc { get; set; }
    public string? RejectedBy { get; set; }
    public string IdeaSnapshotJson { get; set; } = "{}";

    // --- NEW: Deduplication metadata (for smart dedup across runs) ---
    public string CanonicalTopic { get; set; } = string.Empty;
    public string Angle { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string PainPoint { get; set; } = string.Empty;
}
