using System;

namespace ContentOS.Application.Abstractions;

public class RetrievalResult
{
	public string Collection { get; set; } = "";

	public string SourceFile { get; set; } = "";

	public double Score { get; set; }

	public string Title { get; set; } = "";

	public string Snippet { get; set; } = "";

	public string FullContent { get; set; } = "";

	public int EstimatedTokens => Snippet.Length / 4;

	public DateTime? LastModified { get; set; }

	public override string ToString()
	{
		return $"[{Collection}] {Title} (score: {Score:P0}, ~{EstimatedTokens} tokens)";
	}
}
