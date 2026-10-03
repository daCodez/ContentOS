using System.Collections.Generic;

namespace ContentOS.Application.Abstractions;

public class RetrievalQuery
{
	public string Query { get; set; } = "";

	public List<string> Collections { get; set; } = new List<string>();

	public int MaxResults { get; set; } = 5;

	public double MinScore { get; set; } = 0.3;

	public int MaxContextChars { get; set; } = 3000;

	public bool IncludeFullContent { get; set; } = false;
}
