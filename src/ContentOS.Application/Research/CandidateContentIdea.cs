namespace ContentOS.Application.Research;

public class CandidateContentIdea
{
    public string ContentType { get; set; } = "InformationalArticle";
    public string IntentType { get; set; } = "Informational";
    public string ContentBucket { get; set; } = "Informational";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PrimaryKeyword { get; set; } = string.Empty;
    public List<string> SecondaryKeywords { get; set; } = [];
    public string SearchIntent { get; set; } = string.Empty;
    public string AudiencePainPoint { get; set; } = string.Empty;
    public string AudienceGoal { get; set; } = string.Empty;
    public string RecommendedAngle { get; set; } = string.Empty;
    public string WhyNow { get; set; } = string.Empty;
    public bool Evergreen { get; set; }
    public bool Seasonal { get; set; }
    public decimal AudienceFitScore { get; set; }
    public decimal SeoOpportunityScore { get; set; }
    public decimal CompetitionDifficultyScore { get; set; }
    public decimal MonetizationFitScore { get; set; }
    public decimal OverallScore { get; set; }

    // --- Topic diversity fields ---
    public TopicType TopicType { get; set; } = TopicType.Problem;
    public string TopicTypeLabel { get; set; } = "Problem";
    public string SpecificityTag { get; set; } = string.Empty;
    public decimal IntentMatchScore { get; set; }
    public decimal UniquenessScore { get; set; }
    public decimal ClickPotentialScore { get; set; }
    public decimal MonetizationPotentialScore { get; set; }

    // --- Competition & difficulty fields ---
    public bool SerpChecked { get; set; }
    public int AuthorityDomainsInSerp { get; set; }
    public bool IsHighCompetition { get; set; }
    public decimal CompetitionModifier { get; set; } = 1m;
    public decimal LowCompetitionBoost { get; set; }
    public bool MonetizationViable { get; set; } = true;
    public string? MonetizationWarning { get; set; }

    public List<ResearchFinding> SupportingFindings { get; set; } = [];
}