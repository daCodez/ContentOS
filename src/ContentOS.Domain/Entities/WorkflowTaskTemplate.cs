using System;

namespace ContentOS.Domain.Entities;

public class WorkflowTaskTemplate
{
    public Guid Id { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string AssignedAgent { get; set; } = string.Empty;
    public string InstructionsTemplate { get; set; } = string.Empty;
    public string RequiredInputsJson { get; set; } = "[]";
    public string ExpectedOutputsJson { get; set; } = "[]";
    public bool AutoStartWhenPreviousComplete { get; set; } = true;
    public bool IsApprovalRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
