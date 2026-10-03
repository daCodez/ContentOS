using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Agents;

namespace ContentOS.Infrastructure.Writing;

public interface IWorkflowArticleWriter
{
    Task<WorkflowArticleDraft?> ReviseArticleAsync(EditorialRevisionRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<WorkflowArticleDraft?>(new NotSupportedException("Configured writer does not support structured editorial revision."));
    Task<WorkflowArticleDraft?> GenerateDraftAsync(
        string contentType,
        string title,
        string slug,
        string primaryKeyword,
        string summary,
        string searchIntent,
        string audiencePainPoint,
        string audienceGoal,
        string recommendedAngle,
        string whyNow,
        IReadOnlyCollection<string> secondaryKeywords,
        IReadOnlyCollection<string> sourceSummaries,
        int targetWordCountMin,
        int targetWordCountMax,
        TopicExpansionResult? topicExpansion = null,
        ArticleOutlineResult? outline = null,
        string? reworkDirectiveJson = null,
        CancellationToken cancellationToken = default);

    Task<IdeationResponse?> GenerateIdeationResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
