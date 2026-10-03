namespace ContentOS.Application.DTOs;

public class WorkflowDefinitionRunDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionFamilyId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string WorkflowType { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Guid? IdeaRecordId { get; set; }
    public string? IdeaTitle { get; set; }
    public string? ReaderProblem { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public List<WorkflowActionRunDto> Actions { get; set; } = new();
}

public class WorkflowActionRunDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetry { get; set; }
    public string? OutputSnapshotJson { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public List<WorkflowStepRunDto> Steps { get; set; } = new();
}

public class WorkflowStepRunDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

public class WorkflowDefinitionFamilyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ActiveWorkflowDefinitionId { get; set; }
    public List<WorkflowDefinitionDto> Versions { get; set; } = new();
}

public class IdeaRecordDto
{
    public ContentOS.Application.Research.IdeaRankingResult? ReviewedRanking { get; set; }
    public Guid Id { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? SourceWorkflowRunId { get; set; }
    public Guid? ArticleWorkflowRunId { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ApprovedUtc { get; set; }
}
