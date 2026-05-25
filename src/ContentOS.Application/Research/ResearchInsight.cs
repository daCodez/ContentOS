namespace ContentOS.Application.Research;

/// <summary>
/// Abstraction layer between raw research findings and idea generation.
/// Ideas are generated from insights, NOT from source titles.
/// </summary>
public class ResearchInsight
{
    /// <summary>A reader problem or frustration extracted from research.</summary>
    public string PainPoint { get; set; } = string.Empty;

    /// <summary>A recurring pattern observed across multiple sources.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>A content gap — something sources don't cover well.</summary>
    public string Gap { get; set; } = string.Empty;

    /// <summary>An emotional trigger readers experience.</summary>
    public string EmotionalTrigger { get; set; } = string.Empty;

    /// <summary>Source titles that contributed to this insight (for similarity checking).</summary>
    public List<string> SourceTitles { get; set; } = [];

    /// <summary>Keywords/phrases associated with this insight.</summary>
    public List<string> Keywords { get; set; } = [];
}