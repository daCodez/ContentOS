using System;

namespace ContentOS.Application.DTOs;

public class SeoSuggestionDto
{
    public string Suggestion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // High, Medium, Low
    public string Category { get; set; } = string.Empty; // Keywords, Content, Technical, etc.
}