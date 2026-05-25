namespace ContentOS.Infrastructure.Ideation;

public sealed class IdeationResponse
{
    public IList<IdeationDto> Ideas { get; set; } = new List<IdeationDto>();
}

public sealed class IdeationDto
{
    public string Title { get; set; } = string.Empty;
    public string PrimaryKeyword { get; set; } = string.Empty;
    public IList<string> SecondaryKeywords { get; set; } = new List<string>();
    public string SearchIntent { get; set; } = string.Empty;
    public string AudiencePainPoint { get; set; } = string.Empty;
    public string AudienceGoal { get; set; } = string.Empty;
    public string RecommendedAngle { get; set; } = string.Empty;
    public string WhyNow { get; set; } = string.Empty;
    public bool Evergreen { get; set; } = true;
    public bool Seasonal { get; set; } = false;
    public string TopicType { get; set; } = "Problem";
    public string SpecificityTag { get; set; } = "general";
}