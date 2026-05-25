using ContentOS.Application.DTOs;

namespace ContentOS.Application.Abstractions;

public interface IWorkflowMutationService
{
    Task<WorkflowTaskMutationResultDto> InjectManualTaskAsync(Guid workflowJobId, InjectWorkflowTaskRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowControlResultDto> ReopenWorkflowAsync(Guid workflowJobId, ReopenWorkflowRequestDto request, CancellationToken cancellationToken = default);
    Task<WorkflowTaskMutationResultDto> ReorderTaskAsync(Guid workflowJobId, ReorderWorkflowTaskRequestDto request, CancellationToken cancellationToken = default);
}
