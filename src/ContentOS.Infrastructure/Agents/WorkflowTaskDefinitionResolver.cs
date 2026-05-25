using ContentOS.Domain.Entities;

namespace ContentOS.Infrastructure.Agents;

public interface IWorkflowTaskDefinitionResolver
{
    WorkflowTaskDefinition Resolve(ContentWorkflowTask task);
}

public sealed record WorkflowTaskDefinition(
    string ExecutionKey,
    string ArtifactType,
    string? Summary = null,
    string[]? Warnings = null);

public sealed class WorkflowTaskDefinitionResolver : IWorkflowTaskDefinitionResolver
{
    public WorkflowTaskDefinition Resolve(ContentWorkflowTask task)
    {
        if (!string.IsNullOrWhiteSpace(task.AssignedAgent))
        {
            var agentKey = task.AssignedAgent.Trim();
            if (WorkflowTaskRouting.AgentExecutionKeys.TryGetValue(agentKey, out var executionKey))
            {
                var artifactType = WorkflowTaskRouting.ExecutionKeyToArtifactType.TryGetValue(executionKey, out var routedArtifact)
                    ? routedArtifact
                    : "WorkflowArtifact";

                return new WorkflowTaskDefinition(
                    executionKey,
                    artifactType,
                    $"Resolved from assigned agent '{agentKey}'.");
            }
        }

        if (WorkflowTaskRouting.TaskNameToExecutionKey.TryGetValue(task.Name, out var fallbackExecutionKey))
        {
            var artifactType = WorkflowTaskRouting.ExecutionKeyToArtifactType.TryGetValue(fallbackExecutionKey, out var routedArtifact)
                ? routedArtifact
                : "WorkflowArtifact";

            return new WorkflowTaskDefinition(
                fallbackExecutionKey,
                artifactType,
                $"Resolved from legacy task name '{task.Name}'.",
                new[] { "Using legacy task-name routing fallback." });
        }

        return new WorkflowTaskDefinition(
            "default",
            "WorkflowArtifact",
            "No explicit route matched. Using default dispatcher behavior.",
            new[] { "Task routing fell back to default behavior." });
    }
}
