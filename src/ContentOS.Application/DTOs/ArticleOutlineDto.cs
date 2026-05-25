using System;

namespace ContentOS.Application.DTOs;

public class ArticleOutlineDto
{
    public string Title { get; set; } = string.Empty;
    public string[] Headings { get; set; } = Array.Empty<string>();
    public string[] KeyPoints { get; set; } = Array.Empty<string>();
    public string EstimatedLength { get; set; } = string.Empty;
}