using System;

namespace ContentOS.Application.DTOs;

public class HumanizedDto
{
    public string OriginalText { get; set; } = string.Empty;
    public string HumanizedText { get; set; } = string.Empty;
    public double HumanizationScore { get; set; }
    public string ImprovementNotes { get; set; } = string.Empty;
}