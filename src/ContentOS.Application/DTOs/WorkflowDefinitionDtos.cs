namespace ContentOS.Application.DTOs;

public class WorkflowDefinitionDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionFamilyId { get; set; }
    public string WorkflowType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public List<WorkflowActionDefinitionDto> Actions { get; set; } = new();
}

public class WorkflowActionDefinitionDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssignedAgent { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<WorkflowStepDefinitionDto> Steps { get; set; } = new();
}

public class WorkflowStepDefinitionDto
{
    public Guid Id { get; set; }
    public Guid WorkflowActionDefinitionId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class WorkflowDefinitionMutationDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string MutationType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public class WorkflowDefinitionChangeProposalDto
{
    public string UserRequest { get; set; } = string.Empty;
    public string InterpretedIntent { get; set; } = string.Empty;
    public string TargetAction { get; set; } = string.Empty;
    public string ProposedChangeType { get; set; } = string.Empty;
    public WorkflowStepDefinitionDto? ProposedStep { get; set; }
    public int InsertPosition { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> OverlapsExistingSteps { get; set; } = new();
    public string PreviewSummary { get; set; } = string.Empty;
}
