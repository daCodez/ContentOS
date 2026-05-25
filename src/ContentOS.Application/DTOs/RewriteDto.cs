using System;

namespace ContentOS.Application.DTOs;

public class RewriteDto
{
    public string OriginalText { get; set; } = string.Empty;
    public string RewrittenText { get; set; } = string.Empty;
    public string ToneApplied { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}