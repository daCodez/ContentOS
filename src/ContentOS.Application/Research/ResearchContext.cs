namespace ContentOS.Application.Research;

public class ResearchContext
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string AudienceDescription { get; set; } = string.Empty;
    public string MonetizationGoals { get; set; } = string.Empty;
    public string ApprovedWorkflowInstructions { get; set; } = string.Empty;
    public string VerifiedProductContext { get; set; } = string.Empty;
    /// <summary>Actual keep/drop decisions from the current response; serialized with workflow evidence, never inferred from generic angle labels.</summary>
    public List<IdeaSelectionDecision> IdeaSelectionDecisions { get; set; } = [];
    public List<string> SeedTopics { get; set; } = [];
    public int MaxFindingsPerProvider { get; set; } = 5;
    public int MaxIdeasToSave { get; set; } = 8;
    public EditorialRubric IdeaEditorialRubric { get; set; } = EditorialRubric.IdeaProposal;
}

public sealed record IdeaSelectionDecision(int SourceIndex, string Title, bool Retained, string Reason, IReadOnlyList<string> SelectedSourceUrls, IReadOnlyList<string> MatchedSourceUrls, string? MatchedCandidateTitle = null);
