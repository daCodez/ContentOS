using System;
using System.Collections.Generic;

namespace ContentOS.Application.Abstractions;

public class ExtractionResult
{
	public string Title { get; set; } = "";

	public string CleanedText { get; set; } = "";

	public List<string> Headings { get; set; } = new List<string>();

	public List<string> BulletPoints { get; set; } = new List<string>();

	public string SourceUrl { get; set; } = "";

	public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

	public int EstimatedTokens => (Title.Length + CleanedText.Length + string.Join(" ", Headings).Length + string.Join(" ", BulletPoints).Length) / 4;
}
