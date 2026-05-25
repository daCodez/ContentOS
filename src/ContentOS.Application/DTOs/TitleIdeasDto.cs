using System;
using System.Collections.Generic;

namespace ContentOS.Application.DTOs;

public class TitleIdeasDto
{
    public string[] TitleIdeas { get; set; } = Array.Empty<string>();
    public string[] KeywordsUsed { get; set; } = Array.Empty<string>();
    public string TargetAudience { get; set; } = string.Empty;
}