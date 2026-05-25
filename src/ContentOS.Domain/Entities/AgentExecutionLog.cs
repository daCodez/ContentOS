using System;

namespace ContentOS.Domain.Entities;

public class AgentExecutionLog
{
    public Guid Id { get; set; }
    public Guid ContentWorkflowJobId { get; set; }
    public Guid? ContentWorkflowTaskId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
