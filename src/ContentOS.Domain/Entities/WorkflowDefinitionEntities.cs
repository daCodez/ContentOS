namespace ContentOS.Domain.Entities;

public class WorkflowActionDefinition
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssignedAgent { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<WorkflowStepDefinition> Steps { get; set; } = new();
}

public class WorkflowStepDefinition
{
    public Guid Id { get; set; }
    public Guid WorkflowActionDefinitionId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class WorkflowDefinitionMutation
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string MutationType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string OldValueJson { get; set; } = "{}";
    public string NewValueJson { get; set; } = "{}";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
