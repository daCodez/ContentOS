using System;
using System.Collections.Generic;

namespace ContentOS.Domain.Entities;

public class WorkflowTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<WorkflowTemplateTask> Tasks { get; set; } = new();
}

public class WorkflowTemplateTask
{
    public Guid Id { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string AssignedAgent { get; set; } = string.Empty;
    public string DefaultInstructions { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
}
