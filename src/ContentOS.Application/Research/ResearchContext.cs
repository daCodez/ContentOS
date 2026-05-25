namespace ContentOS.Application.Research;

public class ResearchContext
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string AudienceDescription { get; set; } = string.Empty;
    public string MonetizationGoals { get; set; } = string.Empty;
    public List<string> SeedTopics { get; set; } = [];
    public int MaxFindingsPerProvider { get; set; } = 5;
    public int MaxIdeasToSave { get; set; } = 8;
}
