using ContentOS.Domain.Entities;

namespace ContentOS.Infrastructure.Agents;

public interface IWorkflowTaskDispatcher
{
    Task<WorkflowTaskExecutionResult> DispatchAsync(
        ContentWorkflowTask task,
        ContentIdea? idea,
        GeneratedLongformArticle article,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowTaskExecutionResult(
    ContentArtifact Artifact,
    IReadOnlyCollection<ContentArtifact>? SupplementalArtifacts = null,
    ContentArtifact? FinalArticlePreviewArtifact = null,
    string? ResolvedExecutionKey = null,
    string? ExecutionSummary = null,
    IReadOnlyCollection<string>? Warnings = null)
{
    public Guid PrimaryArtifactId => Artifact.Id;
    public Guid? PreviewArtifactId => FinalArticlePreviewArtifact?.Id;
    public IReadOnlyCollection<Guid> SupplementalArtifactIds =>
        (SupplementalArtifacts ?? Array.Empty<ContentArtifact>())
        .Select(x => x.Id)
        .ToArray();
}
