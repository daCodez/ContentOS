using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ContentOS.Infrastructure.Research.SearXng;

[ExcludeFromCodeCoverage]
public class ExtractedContent
{
	public string Title { get; set; } = "";

	public string CleanedText { get; set; } = "";

	public List<string> Headings { get; set; } = new List<string>();

	public List<string> BulletPoints { get; set; } = new List<string>();

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(Title))
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
			handler.AppendLiteral("# ");
			handler.AppendFormatted(Title);
			stringBuilder3.AppendLine(ref handler);
			stringBuilder.AppendLine();
		}
		if (Headings.Count > 0)
		{
			stringBuilder.AppendLine("## Headings");
			foreach (string heading in Headings)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
				handler.AppendLiteral("- ");
				handler.AppendFormatted(heading);
				stringBuilder4.AppendLine(ref handler);
			}
			stringBuilder.AppendLine();
		}
		if (BulletPoints.Count > 0)
		{
			stringBuilder.AppendLine("## Key Points");
			foreach (string bulletPoint in BulletPoints)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
				handler.AppendLiteral("- ");
				handler.AppendFormatted(bulletPoint);
				stringBuilder5.AppendLine(ref handler);
			}
			stringBuilder.AppendLine();
		}
		if (!string.IsNullOrWhiteSpace(CleanedText))
		{
			stringBuilder.AppendLine("## Content");
			stringBuilder.AppendLine(CleanedText);
		}
		return stringBuilder.ToString();
	}
}
