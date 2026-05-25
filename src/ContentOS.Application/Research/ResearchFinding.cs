namespace ContentOS.Application.Research;

public class ResearchFinding
{
    public string ProviderName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ObservedPhrase { get; set; } = string.Empty;
    public string PainPoint { get; set; } = string.Empty;
    public string TopicSuggestion { get; set; } = string.Empty;
    public string KeywordSuggestion { get; set; } = string.Empty;
    public string IntentGuess { get; set; } = string.Empty;
    public string MonetizationHint { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
