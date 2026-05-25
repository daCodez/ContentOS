namespace ContentOS.Infrastructure.Agents;

public sealed record ResearchIntentResult(
    string Topic,
    string PrimaryKeyword,
    string SearchIntent,
    string RecommendedAngle,
    string AudiencePainPoint,
    string AudienceGoal,
    string WhyNow,
    string[] SourceSummaries,
    bool IsQualitySufficient);

public sealed record KeywordStrategyResult(
    string PrimaryKeyword,
    string[] SecondaryKeywords,
    string[] SupportingPhrases,
    string[] TitleOptions,
    string[] FaqQuestions,
    string IntentMatch,
    bool IsQualitySufficient);

public sealed record TopicExpansionResult(
    string CoreIntent,
    string[] Subtopics,
    string[] CoverageRequirements,
    string[] MustAnswerQuestions,
    string[] WorkedExampleIdeas,
    bool IsQualitySufficient);

public sealed record ContentBriefResult(
    string WorkingTitle,
    string[] Brief,
    string[] KeywordCluster,
    string[] Evidence,
    int EstimatedWordCount,
    bool IsQualitySufficient);

public sealed record ArticleOutlineResult(
    string Headline,
    string ContentType,
    int TargetWordCountMin,
    int TargetWordCountMax,
    int EstimatedWordCount,
    string[] RequiredSections,
    OutlineSectionResult[] Sections,
    bool IsQualitySufficient);

public sealed record OutlineSectionResult(
    string Id,
    string Title);

public sealed record EditorialPassResult(
    string Pass,
    string[] Notes,
    int BasedOnDraftWordCount,
    int SectionCount,
    bool IsQualitySufficient);

public sealed record HeadlinePackResult(
    string PrimaryHeadline,
    string[] AlternateHeadlines,
    string RecommendedHeadline,
    string[] HookLines,
    bool IsQualitySufficient);

public sealed record SeoOptimizationResult(
    string TitleTag,
    string MetaDescription,
    string[] KeywordCluster,
    string SearchIntentAlignment,
    string[] RequiredKeywordMentions,
    string[] MissingKeywordMentions,
    string[] InternalLinkRecommendations,
    string[] ExternalAuthorityLinks,
    int DraftWordCount,
    string[] SectionHeadings,
    bool IsQualitySufficient,
    SeoValidationChecklist? ValidationChecklist = null);

public sealed record SeoValidationChecklist(
    bool KeywordInTitle,
    bool KeywordInIntro,
    bool KeywordInHeadings,
    bool FaqSectionExists,
    int FaqCount,
    int InternalLinksCount,
    bool ExternalAuthorityLinksPresent,
    bool ArticleFlowsNaturally,
    int KeywordCoverageScore,
    string[] Failures);

public sealed record InternalLinkPlanResult(
    object[] InternalLinkTargets,
    string[] RecommendedAnchors,
    bool IsQualitySufficient);

public sealed record MonetizationPlanResult(
    string[] Placements,
    string LeadMagnetCta,
    string[] ToolRecommendations,
    string[] FaqQuestions,
    string ToolsSectionHeading,
    bool IsQualitySufficient);

public sealed record QaReportResult(
    string QaStatus,
    decimal HookScore,
    decimal SeoScore,
    decimal ReadabilityScore,
    decimal ValueScore,
    decimal MonetizationScore,
    decimal OverallScore,
    bool RewriteRequired,
    string[] HardRuleFailures,
    string[] Recommendations,
    bool SyntheticFallbackUsed,
    bool IsQualitySufficient,
    ContentOS.Domain.Qa.QaReworkDirective? ReworkDirective = null);
