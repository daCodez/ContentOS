namespace ContentOS.Application.Research;

public class RunResearchResult
{
    public Guid SiteId { get; set; }
    public int RequestedIdeaCount { get; set; }
    public int FindingsGathered { get; set; }
    public int CandidatesCreated { get; set; }
    public int IdeasSaved { get; set; }
    public int DuplicatesSkipped { get; set; }
    public List<string> SavedTitles { get; set; } = [];
    public string? ShortfallReason { get; set; }
}
