using ContentOS.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Infrastructure.Agents;

public sealed class WorkflowTaskDispatcher : IWorkflowTaskDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkflowTaskDefinitionResolver _definitionResolver;

    public WorkflowTaskDispatcher(IServiceScopeFactory scopeFactory, IWorkflowTaskDefinitionResolver definitionResolver)
    {
        _scopeFactory = scopeFactory;
        _definitionResolver = definitionResolver;
    }

    public async Task<WorkflowTaskExecutionResult> DispatchAsync(
        ContentWorkflowTask task,
        ContentIdea? idea,
        GeneratedLongformArticle article,
        CancellationToken cancellationToken = default)
    {
        // Resolve coordinator lazily to break the circular dependency:
        // WorkflowCoordinatorAgent -> IWorkflowTaskDispatcher -> WorkflowTaskDispatcher -> IWorkflowCoordinatorAgent
        using var scope = _scopeFactory.CreateScope();
        var coordinator = (WorkflowCoordinatorAgent)scope.ServiceProvider.GetRequiredService<IWorkflowCoordinatorAgent>();

        var definition = _definitionResolver.Resolve(task);

        var payload = definition.ExecutionKey switch
        {
            "research-intent" => await coordinator.BuildResearchIntentPayloadAsync(task, idea, article, cancellationToken),
            "keyword-strategy" => await coordinator.BuildKeywordStrategyPayloadAsync(task, idea, article, cancellationToken),
            "topic-expansion" => await coordinator.BuildTopicExpansionPayloadAsync(task, idea, article, cancellationToken),
            "optimized-seo-package" => await coordinator.BuildOptimizedSeoPayloadAsync(task, idea, article, cancellationToken),
            "structured-outline" => await coordinator.BuildStructuredOutlinePayloadAsync(task, idea, article, cancellationToken),
            "draft-article" => await coordinator.BuildDraftPayloadAsync(task, idea, article, cancellationToken),
            "seo-linking" => await coordinator.BuildSeoLinkingPayloadAsync(task, idea, article, cancellationToken),
            "monetization-cta" => await coordinator.BuildMonetizationPayloadAsync(task, idea, article, cancellationToken),
            "strict-qa" => await coordinator.BuildStrictQaPayloadAsync(task, idea, article, cancellationToken),
            "humanize-final" => await coordinator.BuildHumanizedPayloadAsync(task, idea, article, cancellationToken),
            "light-qa" => await coordinator.BuildLightQaPayloadAsync(task, idea, article, cancellationToken),
            _ => await coordinator.BuildDefaultPayloadAsync(task, idea, article, cancellationToken)
        };

        var artifact = coordinator.CreateArtifactForDispatch(task, payload, definition.ArtifactType);
        var supplementalArtifacts = (await coordinator.BuildSupplementalArtifactsForDispatchAsync(task, idea, article, cancellationToken)).ToArray();
        var previewArtifact = coordinator.BuildFinalArticlePreviewArtifactForDispatch(task, idea, article);

        return new WorkflowTaskExecutionResult(
            artifact,
            supplementalArtifacts,
            previewArtifact,
            definition.ExecutionKey,
            definition.Summary,
            definition.Warnings);
    }
}