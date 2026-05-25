using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Research.Serp;

namespace ContentOS.Infrastructure.Scoring;

public interface IOptimizationScoringService
{
    OptimizationScoreReport Compute(ContentIdea idea, GeneratedLongformArticle article, SerpBenchmarkReport benchmark);
}

public sealed class OptimizationScoreReport
{
    public int OptimizationScore { get; set; }
    public int HighPriorityScore { get; set; }
    public int LowPriorityScore { get; set; }
    public int StuffingRiskScore { get; set; }
    public int StuffingPenaltyApplied { get; set; }
    public bool PublishReady { get; set; }

    public int IntentMatchScore { get; set; }
    public int ContentQualityScore { get; set; }
    public int KeywordOptimizationScore { get; set; }
    public int HeadlineCtrScore { get; set; }
    public int MonetizationReadinessScore { get; set; }
    public int TopicDepthScore { get; set; }

    public int InternalLinkingScore { get; set; }
    public int MediaScore { get; set; }
    public int UxReadabilityScore { get; set; }
    public int SchemaTechnicalSeoScore { get; set; }
    public int FreshnessScore { get; set; }

    public int PrimaryKeywordScore { get; set; }
    public int SynonymCoverageScore { get; set; }
    public int ContentStructureScore { get; set; }
    public int ContentLengthScore { get; set; }
    public int LinksScore { get; set; }
    public int ChecklistScore { get; set; }
    public int PlacementQualityScore { get; set; }
    public int NaturalnessScore { get; set; }

    public int ActualWordCount { get; set; }
    public int TargetWordCountMin { get; set; }
    public int TargetWordCountMax { get; set; }
    public int ActualPrimaryKeywordCount { get; set; }
    public int TargetPrimaryKeywordMin { get; set; }
    public int TargetPrimaryKeywordMax { get; set; }
    public decimal PrimaryKeywordDensity { get; set; }
    public int ActualLinksCount { get; set; }
    public int TargetLinksMin { get; set; }
    public int TargetLinksMax { get; set; }
    public int ActualMediaCount { get; set; }
    public int TargetMediaMin { get; set; }
    public int TargetMediaMax { get; set; }
    public int CompletedChecklistItems { get; set; }
    public int TargetChecklistMin { get; set; }
    public int TargetChecklistMax { get; set; }
    public int SynonymCoverageFoundCount { get; set; }
    public int TargetSynonymCoverageCount { get; set; }

    public List<string> MissingRelatedKeywords { get; set; } = [];
    public List<string> OverusedKeywords { get; set; } = [];
    public List<KeywordChip> PrimaryKeywordChips { get; set; } = [];
    public List<KeywordChip> RelatedKeywordChips { get; set; } = [];

    public string StatusLabel => OptimizationScore switch
    {
        >= 90 => "Elite",
        >= 80 => "Strong",
        >= 70 => "Good",
        >= 60 => "Fair",
        >= 50 => "Optimization Opportunity",
        _ => "Poor"
    };

    public string StuffingRiskLabel => StuffingRiskScore switch
    {
        <= 20 => "Safe",
        <= 35 => "Warning",
        <= 45 => "High Risk",
        _ => "FAIL"
    };
}

public sealed class KeywordChip
{
    public string Keyword { get; set; } = string.Empty;
    public int ActualCount { get; set; }
    public int TargetMin { get; set; }
    public int TargetMax { get; set; }
    public int Score { get; set; }
    public string Status => Score switch
    {
        >= 90 => "on-target",
        >= 70 => "near-target",
        _ => ActualCount > TargetMax ? "overused" : "off-target"
    };
}
