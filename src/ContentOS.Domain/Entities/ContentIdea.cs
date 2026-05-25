using System;

namespace ContentOS.Domain.Entities;

public class ContentIdea
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SlugSuggestion { get; set; } = string.Empty;
    public string PrimaryKeyword { get; set; } = string.Empty;
    public string SecondaryKeywordsJson { get; set; } = "[]";
    public string SearchIntent { get; set; } = string.Empty;
    public string AudiencePainPoint { get; set; } = string.Empty;
    public string AudienceGoal { get; set; } = string.Empty;
    public string RecommendedAngle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string WhyNow { get; set; } = string.Empty;
    public string ContentType { get; set; } = "LongFormBlogArticle";
    public string FunnelStage { get; set; } = string.Empty;
    public decimal MonetizationFitScore { get; set; }
    public decimal SeoOpportunityScore { get; set; }
    public decimal TrendScore { get; set; }
    public decimal CompetitionScore { get; set; }
    public decimal OverallScore { get; set; }
    public bool Evergreen { get; set; }
    public bool Seasonal { get; set; }
    public string SourceSummaryJson { get; set; } = "[]";
    public string Status { get; set; } = "Discovered";

    // --- Topic diversity fields ---
    public string TopicType { get; set; } = "Problem";
    public string SpecificityTag { get; set; } = string.Empty;
    public decimal IntentMatchScore { get; set; }
    public decimal UniquenessScore { get; set; }
    public decimal ClickPotentialScore { get; set; }
    public decimal MonetizationPotentialScore { get; set; }

    // --- NEW: Deduplication metadata (for smart dedup across runs) ---
    public string CanonicalTopic { get; set; } = string.Empty;
    public string Angle { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string PainPoint { get; set; } = string.Empty;

    // --- Competition & difficulty fields ---
    public bool IsHighCompetition { get; set; }
    public decimal CompetitionModifier { get; set; } = 1m;
    public decimal LowCompetitionBoost { get; set; }
    public bool MonetizationViable { get; set; } = true;
    public string? MonetizationWarning { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedUtc { get; set; }
    public DateTime? RejectedUtc { get; set; }
    public string? ApprovedBy { get; set; }
}