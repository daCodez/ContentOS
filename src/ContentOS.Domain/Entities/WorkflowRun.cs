using System;

namespace ContentOS.Domain.Entities;

public class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    
    public virtual Workflow Workflow { get; set; } = null!;
}