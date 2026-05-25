using ContentOS.Domain.Enums;

namespace ContentOS.Domain.Entities;

public class WorkflowDefinition
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionFamilyId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkflowDefinitionType WorkflowType { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime PublishedUtc { get; set; } = DateTime.UtcNow;
    public string PublishedBy { get; set; } = string.Empty;
}
