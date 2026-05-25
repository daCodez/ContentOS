using System;

namespace ContentOS.Application.DTOs;

public class SeoAnalysisDto
{
    public int Score { get; set; }
    public string TitleLength { get; set; } = string.Empty;
    public string MetaDescriptionLength { get; set; } = string.Empty;
    public string HeadingStructure { get; set; } = string.Empty;
    public string KeywordUsage { get; set; } = string.Empty;
    public string ReadabilityScore { get; set; } = string.Empty;
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}