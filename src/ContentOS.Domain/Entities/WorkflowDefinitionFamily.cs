using ContentOS.Domain.Enums;

namespace ContentOS.Domain.Entities;

public class WorkflowDefinitionFamily
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public WorkflowDefinitionType WorkflowType { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ActiveWorkflowDefinitionId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
