using ContentOS.Application.DTOs;

namespace ContentOS.Application.Abstractions;

public interface IWorkflowControlService
{
    Task<WorkflowControlResultDto> ExecuteTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> ResetTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> AcceptTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> RejectTaskAsync(Guid workflowJobId, Guid taskId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> PauseWorkflowAsync(Guid workflowJobId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> ResumeWorkflowAsync(Guid workflowJobId, WorkflowTaskControlRequestDto request, CancellationToken cancellationToken = default);
}
