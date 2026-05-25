namespace ContentOS.Infrastructure.Agents;

public static class AgentStack
{
    public const string WorkflowCoordinator = "orchestrator";
    public const string ResearchAgent = "content-research";
    public const string KeywordAgent = "keyword-strategy";
    public const string ContentPlannerAgent = "content-planner";
    public const string WriterAgent = "content-writer";
    public const string SeoOptimizerAgent = "content-seo";
    public const string MonetizationAgent = "content-monetization";
    public const string QaScoringAgent = "content-scoring";
    public const string HumanizerAgent = "content-humanizer";
    public const string LightQaAgent = "content-light-qa";
    public const string QaFixVerificationAgent = "content-qa-fix-verification";

    public const string ContentStrategyAgent = ContentPlannerAgent;
    public const string EditorialAgent = HumanizerAgent;
    public const string SeoAndMonetizationAgent = SeoOptimizerAgent;
    public const string VisualAndPublishingAgent = "content-media";
    public const string QaAndComplianceAgent = QaScoringAgent;
    public const string SeoOptimizationLoopAgent = "seo-optimization-loop";

    public static readonly IReadOnlyList<string> CanonicalAgents =
    [
        WorkflowCoordinator,
        ResearchAgent,
        KeywordAgent,
        ContentPlannerAgent,
        WriterAgent,
        SeoOptimizerAgent,
        MonetizationAgent,
        QaScoringAgent,
        HumanizerAgent,
        LightQaAgent,
        ContentStrategyAgent,
        EditorialAgent,
        SeoAndMonetizationAgent,
        VisualAndPublishingAgent,
        QaAndComplianceAgent
    ];
}
