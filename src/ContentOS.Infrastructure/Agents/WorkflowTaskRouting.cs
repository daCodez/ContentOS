using ContentOS.Domain.Entities;

namespace ContentOS.Infrastructure.Agents;

internal sealed record WorkflowTaskRoute(
    string ArtifactType,
    Func<ContentWorkflowTask, ContentIdea?, GeneratedLongformArticle, CancellationToken, Task<object>> BuildPayloadAsync);

public static class WorkflowTaskRouting
{
    public static readonly IReadOnlyDictionary<string, string> TaskNameToExecutionKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Research Topic and Intent"] = "research-intent",
            ["Build Keyword Strategy"] = "keyword-strategy",
            ["Expand Topic Coverage"] = "topic-expansion",
            ["Optimize SEO Package"] = "optimized-seo-package",
            ["Create Structured Outline"] = "structured-outline",
            ["Write Rule-Compliant Draft"] = "draft-article",
            ["Apply SEO and Linking"] = "seo-linking",
            ["Add Monetization and CTA"] = "monetization-cta",
            ["Run Strict QA Scoring"] = "strict-qa",
            ["Humanize Final Draft"] = "humanize-final",
            ["Run Light QA Check"] = "light-qa"
        };

    public static readonly IReadOnlyDictionary<string, string> ExecutionKeyToArtifactType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["research-intent"] = "ResearchIntent",
            ["keyword-strategy"] = "KeywordStrategy",
            ["topic-expansion"] = "TopicExpansion",
            ["optimized-seo-package"] = "OptimizedSeoPackage",
            ["structured-outline"] = "ArticleOutline",
            ["draft-article"] = "DraftArticle",
            ["seo-linking"] = "SeoOptimization",
            ["monetization-cta"] = "MonetizationPlan",
            ["strict-qa"] = "QaReport",
            ["humanize-final"] = "PublishReadyPackage",
            ["light-qa"] = "LightQaReport",
            ["default"] = "WorkflowArtifact"
        };

    public static readonly IReadOnlyDictionary<string, string> AgentExecutionKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AgentStack.ResearchAgent] = "research-intent",
            [AgentStack.KeywordAgent] = "keyword-strategy",
            [AgentStack.SeoOptimizationLoopAgent] = "optimized-seo-package",
            [AgentStack.ContentPlannerAgent] = "structured-outline",
            [AgentStack.WriterAgent] = "draft-article",
            [AgentStack.SeoOptimizerAgent] = "seo-linking",
            [AgentStack.MonetizationAgent] = "monetization-cta",
            [AgentStack.QaScoringAgent] = "strict-qa",
            [AgentStack.HumanizerAgent] = "humanize-final",
            [AgentStack.LightQaAgent] = "light-qa"
        };
}
