using System.Text;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Workflow;

public sealed class WorkflowExportService : IWorkflowExportService
{
    private readonly ContentOsDbContext _dbContext;

    public WorkflowExportService(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowExportDto?> ExportAsync(Guid workflowJobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ContentWorkflowJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workflowJobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var tasks = await _dbContext.ContentWorkflowTasks
            .AsNoTracking()
            .Where(x => x.ContentWorkflowJobId == workflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        return new WorkflowExportDto
        {
            WorkflowJobId = job.Id,
            JobStatus = job.Status,
            CurrentStage = job.CurrentStage,
            StartedUtc = job.StartedUtc,
            CompletedUtc = job.CompletedUtc,
            Tasks = tasks.Select(MapTask).ToArray()
        };
    }

    public async Task<string?> ExportMarkdownAsync(Guid workflowJobId, CancellationToken cancellationToken = default)
    {
        var export = await ExportAsync(workflowJobId, cancellationToken);
        if (export is null)
        {
            return null;
        }

        var markdown = new StringBuilder();
        markdown.AppendLine($"# Workflow Export {export.WorkflowJobId}");
        markdown.AppendLine();
        markdown.AppendLine($"- Status: {export.JobStatus}");
        markdown.AppendLine($"- Current Stage: {export.CurrentStage}");
        markdown.AppendLine($"- Started UTC: {FormatDate(export.StartedUtc)}");
        markdown.AppendLine($"- Completed UTC: {FormatDate(export.CompletedUtc)}");
        markdown.AppendLine();
        markdown.AppendLine("## Tasks");
        markdown.AppendLine();

        foreach (var task in export.Tasks)
        {
            markdown.AppendLine($"### {task.DisplayOrder}. {task.Name}");
            markdown.AppendLine($"- Stage: {task.StageName}");
            markdown.AppendLine($"- Assigned Agent: {task.AssignedAgent}");
            markdown.AppendLine($"- Status: {task.Status}");
            markdown.AppendLine($"- Scope: {task.ScopeType}{(string.IsNullOrWhiteSpace(task.ScopeId) ? string.Empty : $" ({task.ScopeId})")}");
            markdown.AppendLine($"- Resolved Execution Key: {task.ResolvedExecutionKey ?? "(none)"}");
            markdown.AppendLine($"- Result Artifact Id: {task.ResultArtifactId ?? "(none)"}");
            markdown.AppendLine($"- Preview Artifact Id: {(task.PreviewArtifactId?.ToString() ?? "(none)")}");
            markdown.AppendLine($"- Supplemental Artifact Ids: {(task.SupplementalArtifactIds.Count == 0 ? "(none)" : string.Join(", ", task.SupplementalArtifactIds))}");
            markdown.AppendLine($"- Execution Summary: {task.ExecutionSummary ?? "(none)"}");
            markdown.AppendLine($"- Warnings: {(task.Warnings.Count == 0 ? "(none)" : string.Join(" | ", task.Warnings))}");
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private static WorkflowTaskExportDto MapTask(ContentOS.Domain.Entities.ContentWorkflowTask task)
    {
        string? resolvedExecutionKey = null;
        string? executionSummary = null;
        string[] warnings = [];
        Guid? previewArtifactId = null;
        Guid[] supplementalArtifactIds = [];

        if (!string.IsNullOrWhiteSpace(task.InputDataJson) && task.InputDataJson != "{}")
        {
            using var document = JsonDocument.Parse(task.InputDataJson);
            var root = document.RootElement;

            if (root.TryGetProperty("resolvedExecutionKey", out var resolvedElement))
            {
                resolvedExecutionKey = resolvedElement.GetString();
            }

            if (root.TryGetProperty("executionSummary", out var summaryElement))
            {
                executionSummary = summaryElement.GetString();
            }

            if (root.TryGetProperty("warnings", out var warningsElement) && warningsElement.ValueKind == JsonValueKind.Array)
            {
                warnings = warningsElement.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
            }

            if (root.TryGetProperty("previewArtifactId", out var previewElement) && previewElement.ValueKind == JsonValueKind.String && Guid.TryParse(previewElement.GetString(), out var parsedPreview))
            {
                previewArtifactId = parsedPreview;
            }

            if (root.TryGetProperty("supplementalArtifactIds", out var supplementalElement) && supplementalElement.ValueKind == JsonValueKind.Array)
            {
                supplementalArtifactIds = supplementalElement.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                    .Where(x => Guid.TryParse(x, out _))
                    .Select(x => Guid.Parse(x!))
                    .ToArray();
            }
        }

        return new WorkflowTaskExportDto
        {
            TaskId = task.Id,
            Name = task.Name,
            StageName = task.StageName,
            DisplayOrder = task.DisplayOrder,
            AssignedAgent = task.AssignedAgent,
            Status = task.Status.ToString(),
            ScopeType = task.ScopeType.ToString(),
            ScopeId = task.ScopeId,
            ResultArtifactId = task.ResultArtifactId,
            ResolvedExecutionKey = resolvedExecutionKey,
            ExecutionSummary = executionSummary,
            Warnings = warnings,
            PreviewArtifactId = previewArtifactId,
            SupplementalArtifactIds = supplementalArtifactIds
        };
    }

    private static string FormatDate(DateTime? value) => value?.ToString("O") ?? "(not set)";
}
