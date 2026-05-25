namespace ContentOS.Infrastructure.Agents;

public sealed record OptimizedSeoPackage(
    string PrimaryKeyword,
    string[] TopTitles,
    string BestTitle,
    string BestMetaDescription,
    string[] MetaDescriptionOptions,
    string[] SecondaryKeywords,
    string[] SemanticKeywords,
    string[] FaqKeywords,
    string[] FaqQuestions,
    string KeywordClusterSummary,
    bool MeetsMinimums,
    bool IsQualitySufficient);

public sealed record TitleScore(
    string Title,
    decimal KeywordPresence,
    decimal ClickPotential,
    decimal Clarity,
    decimal Specificity,
    decimal Total);