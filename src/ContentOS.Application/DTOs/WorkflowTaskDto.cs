using System;

namespace ContentOS.Application.DTOs;

public class WorkflowTaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string AssignedAgent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
}
