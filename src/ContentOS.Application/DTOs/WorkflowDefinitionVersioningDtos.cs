namespace ContentOS.Application.DTOs;

public class WorkflowDefinitionMutationHistoryDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string MutationType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public class WorkflowJobVersionInfoDto
{
    public Guid WorkflowJobId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string WorkflowDefinitionName { get; set; } = string.Empty;
    public int WorkflowDefinitionVersion { get; set; }
}
