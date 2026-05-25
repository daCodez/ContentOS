using ContentOS.Application.DTOs;

namespace ContentOS.Application.Abstractions;

public interface IWorkflowExportService
{
    Task<WorkflowExportDto?> ExportAsync(Guid workflowJobId, CancellationToken cancellationToken = default);
    Task<string?> ExportMarkdownAsync(Guid workflowJobId, CancellationToken cancellationToken = default);
}
