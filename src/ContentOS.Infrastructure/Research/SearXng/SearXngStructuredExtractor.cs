using ContentOS.Infrastructure.Workflow;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SearXng;

[ExcludeFromCodeCoverage]
public class SearXngStructuredExtractor
{
	private readonly HttpClient _httpClient;

	private readonly ILogger<SearXngStructuredExtractor> _logger;

	private readonly HashSet<string> _recentlyFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private DateTime _lastFailurePurge = DateTime.UtcNow;

	public SearXngStructuredExtractor(HttpClient httpClient, ILogger<SearXngStructuredExtractor> logger)
	{
		_httpClient = httpClient;
		_logger = logger;
	}

	public async Task<ExtractedContent?> ExtractAsync(string url, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}
		PurgeOldFailures();
		if (_recentlyFailed.Contains(url))
		{
			return null;
		}
		try
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(8L));
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Add("User-Agent", "ContentOS/1.0 (Research Bot; +https://contentos.ai)");
			request.Headers.Add("Accept", "text/html,application/xhtml+xml");
			request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
			using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			response.EnsureSuccessStatusCode();
			string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
			if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) && !contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			ExtractedContent result;
			await using (Stream stream = await response.Content.ReadAsStreamAsync(ct))
			{
				using StreamReader reader = new StreamReader(stream);
				result = ParseHtml(await reader.ReadToEndAsync());
			}
			return result;
		}
		catch (HttpRequestException exception)
		{
			WorkflowDiagnostics.LogFailure(_logger, exception, "Structured extraction failed. No page text was accepted.", WorkflowDiagnostics.SourceId(url));
			_recentlyFailed.Add(url);
			return null;
		}
		catch (TaskCanceledException)
		{
			_recentlyFailed.Add(url);
			return null;
		}
		catch (Exception exception2)
		{
			WorkflowDiagnostics.LogFailure(_logger, exception2, "Structured extraction failed. No page text was accepted.", WorkflowDiagnostics.SourceId(url));
			return null;
		}
	}

	public static ExtractedContent? ParseHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return null;
		}
		string text = ExtractTag(html, "title")?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			text = ExtractMetaContent(html, "og:title")?.Trim() ?? "";
		}
		string input = RemoveBoilerplate(html);
		List<string> list = new List<string>();
		string[] array = new string[3] { "h1", "h2", "h3" };
		foreach (string value in array)
		{
			string pattern = $"<{value}[^>]*>(.*?)</{value}>";
			foreach (Match item in Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
			{
				string text2 = StripTags(item.Groups[1].Value).Trim();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					list.Add(text2);
				}
			}
		}
		List<string> list2 = new List<string>();
		string pattern2 = "<li[^>]*>(.*?)</li>";
		foreach (Match item2 in Regex.Matches(input, pattern2, RegexOptions.IgnoreCase | RegexOptions.Singleline))
		{
			string text3 = StripTags(item2.Groups[1].Value).Trim();
			if (!string.IsNullOrWhiteSpace(text3) && text3.Length > 5)
			{
				list2.Add(text3);
			}
		}
		List<string> list3 = new List<string>();
		string pattern3 = "<p[^>]*>(.*?)</p>";
		foreach (Match item3 in Regex.Matches(input, pattern3, RegexOptions.IgnoreCase | RegexOptions.Singleline))
		{
			string text4 = StripTags(item3.Groups[1].Value).Trim();
			if (!string.IsNullOrWhiteSpace(text4) && text4.Length > 25)
			{
				list3.Add(text4);
			}
		}
		string text5 = BuildCleanedText(list3, 2000);
		list = list.Take(15).ToList();
		list2 = list2.Take(20).ToList();
		if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(text5) && list.Count == 0 && list2.Count == 0)
		{
			return null;
		}
		return new ExtractedContent
		{
			Title = (text ?? ""),
			CleanedText = text5,
			Headings = list,
			BulletPoints = list2
		};
	}

	private static string RemoveBoilerplate(string html)
	{
		string input = html;
		input = Regex.Replace(input, "<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		input = Regex.Replace(input, "<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		string[] array = new string[6] { "nav", "footer", "header", "aside", "form", "noscript" };
		foreach (string value in array)
		{
			input = Regex.Replace(input, $"<{value}[^>]*>.*?</{value}>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		}
		return Regex.Replace(input, "<div[^>]*(class|id)\\s*=\\s*[\"'][^\"']*(sidebar|footer|nav|menu|comment|ad|advertisement|cookie|popup|modal|banner)[^\"']*[\"'][^>]*>.*?</div>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
	}

	private static string ExtractTag(string html, string tagName)
	{
		string pattern = $"<{tagName}[^>]*>(.*?)</{tagName}>";
		Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
		return match.Success ? StripTags(match.Groups[1].Value) : null;
	}

	private static string? ExtractMetaContent(string html, string property)
	{
		string pattern = "<meta[^>]*property\\s*=\\s*[\"']" + Regex.Escape(property) + "[\"'][^>]*content\\s*=\\s*[\"'](.*?)[\"']";
		Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			pattern = "<meta[^>]*content\\s*=\\s*[\"'](.*?)[\"'][^>]*property\\s*=\\s*[\"']" + Regex.Escape(property) + "[\"']";
			match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
		}
		return match.Success ? match.Groups[1].Value : null;
	}

	private static string StripTags(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return "";
		}
		char[] array = new char[html.Length];
		int length = 0;
		bool flag = false;
		foreach (char c in html)
		{
			switch (c)
			{
			case '<':
				flag = true;
				continue;
			case '>':
				flag = false;
				continue;
			}
			if (!flag)
			{
				array[length++] = c;
			}
		}
		string value = new string(array, 0, length);
		value = WebUtility.HtmlDecode(value);
		return Regex.Replace(value, "\\s+", " ").Trim();
	}

	private static string BuildCleanedText(List<string> paragraphs, int maxChars)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = maxChars;
		foreach (string paragraph in paragraphs)
		{
			if (num <= 0)
			{
				break;
			}
			string text = ((paragraph.Length <= num) ? paragraph : paragraph.Substring(0, Math.Min(num, paragraph.Length)).TrimEnd());
			stringBuilder.AppendLine(text);
			num -= text.Length + 1;
		}
		return stringBuilder.ToString().Trim();
	}

	private void PurgeOldFailures()
	{
		if (!((DateTime.UtcNow - _lastFailurePurge).TotalMinutes < 5.0))
		{
			_recentlyFailed.Clear();
			_lastFailurePurge = DateTime.UtcNow;
		}
	}
}
