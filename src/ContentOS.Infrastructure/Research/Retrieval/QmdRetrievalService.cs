using ContentOS.Infrastructure.Workflow;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.Retrieval;

public class QmdRetrievalService : IRetrievalService
{
	private readonly ILogger<QmdRetrievalService> _logger;

	private readonly string _qmdBinary;

	private readonly string _collectionsBasePath;

	private bool? _availabilityCache;

	public bool IsAvailable
	{
		get
		{
			if (_availabilityCache.HasValue)
			{
				return _availabilityCache.Value;
			}
			try
			{
				_availabilityCache = RunQmd("status", "", 5000).ExitCode == 0;
			}
			catch
			{
				_availabilityCache = false;
			}
			if (!_availabilityCache.Value)
			{
				_logger.LogWarning("QMD is unavailable. Retrieval will return no results; check the local retrieval setup.");
			}
			return _availabilityCache.Value;
		}
	}

	public QmdRetrievalService(ILogger<QmdRetrievalService> logger)
	{
		_logger = logger;
		_qmdBinary = FindQmdBinary() ?? "qmd";
		_collectionsBasePath = Environment.GetEnvironmentVariable("QMD_COLLECTIONS_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qmd-memory", "collections");
	}

	public async Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(RetrievalQuery query, CancellationToken ct = default(CancellationToken))
	{
		if (!IsAvailable)
		{
			return Array.Empty<RetrievalResult>();
		}
		if (string.IsNullOrWhiteSpace(query.Query))
		{
			return Array.Empty<RetrievalResult>();
		}
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			StringBuilder args = new StringBuilder($"search \"{query.Query.Replace("\"", "\\\"")}\" --json -n {query.MaxResults}");
			if (query.Collections.Count > 0)
			{
				foreach (string col in query.Collections)
				{
					StringBuilder stringBuilder = args;
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 1, stringBuilder);
					handler.AppendLiteral(" -c ");
					handler.AppendFormatted(col);
					stringBuilder2.Append(ref handler);
				}
			}
			if (query.MinScore > 0.0)
			{
				StringBuilder stringBuilder = args;
				StringBuilder stringBuilder3 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder);
				handler.AppendLiteral(" --min-score ");
				handler.AppendFormatted(query.MinScore, "F1");
				stringBuilder3.Append(ref handler);
			}
			(string Output, int ExitCode) result = await RunQmdAsync(args.ToString(), null, 15000, ct);
			if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
			{
				_logger.LogDebug("QMD search returned no results. Review the collected research before relying on retrieval.");
				return Array.Empty<RetrievalResult>();
			}
			IReadOnlyList<RetrievalResult> parsed = ParseQmdJsonOutput(result.Output);
			sw.Stop();
			_logger.LogInformation("QMD retrieval: → {Count} results in {Ms}ms, {TotalChars} chars before budgeting", parsed.Count, sw.ElapsedMilliseconds, parsed.Sum((RetrievalResult r) => r.Snippet.Length));
			IReadOnlyList<RetrievalResult> budgeted = ApplyContextBudget(parsed, query.MaxContextChars, query.IncludeFullContent);
			_logger.LogInformation("QMD retrieval: after budgeting → {Count} results, {TotalChars} chars, ~{Tokens} tokens", budgeted.Count, budgeted.Sum((RetrievalResult r) => r.Snippet.Length), budgeted.Sum((RetrievalResult r) => r.EstimatedTokens));
			return budgeted;
		}
		catch (Exception exception)
		{
			WorkflowDiagnostics.LogFailure(_logger, exception, "Local retrieval failed. No retrieved evidence was accepted.");
			return Array.Empty<RetrievalResult>();
		}
	}

	public async Task IndexAsync(string collection, string slug, string markdownContent, CancellationToken ct = default(CancellationToken))
	{
		if (IsAvailable)
		{
			string dir = Path.Combine(_collectionsBasePath, collection);
			Directory.CreateDirectory(dir);
			string filePath = Path.Combine(dir, slug.EndsWith(".md") ? slug : (slug + ".md"));
			await File.WriteAllTextAsync(filePath, markdownContent, ct);
			_availabilityCache = null;
			await RunQmdAsync("update", null, 30000, ct);
			await RunQmdAsync("embed", null, 60000, ct);
			_logger.LogInformation("Local content indexing completed. IndexedCharacters={IndexedCharacters}", markdownContent.Length);
		}
	}

	public async Task RemoveAsync(string collection, string slug, CancellationToken ct = default(CancellationToken))
	{
		if (IsAvailable)
		{
			string filePath = Path.Combine(path3: slug.EndsWith(".md") ? slug : (slug + ".md"), path1: _collectionsBasePath, path2: collection);
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				await RunQmdAsync("update", null, 30000, ct);
				_logger.LogInformation("Local indexed content removal completed.");
			}
			else
			{
				_logger.LogDebug("Indexed content removal found no local file.");
			}
		}
	}

	private static IReadOnlyList<RetrievalResult> ApplyContextBudget(IReadOnlyList<RetrievalResult> results, int maxContextChars, bool includeFullContent)
	{
		if (results.Count == 0)
		{
			return results;
		}
		List<RetrievalResult> list = new List<RetrievalResult>();
		int num = maxContextChars;
		foreach (RetrievalResult result in results)
		{
			if (num <= 100)
			{
				break;
			}
			string text = (includeFullContent ? result.FullContent : result.Snippet);
			if (string.IsNullOrEmpty(text))
			{
				if (result.Title.Length <= num)
				{
					list.Add(new RetrievalResult
					{
						Collection = result.Collection,
						SourceFile = result.SourceFile,
						Score = result.Score,
						Title = result.Title,
						Snippet = "",
						FullContent = "",
						LastModified = result.LastModified
					});
					num -= result.Title.Length;
				}
				continue;
			}
			if (text.Length <= num)
			{
				list.Add(result);
				num -= text.Length;
				continue;
			}
			string text2 = TrimToBudget(text, num);
			if (text2.Length >= 100)
			{
				list.Add(new RetrievalResult
				{
					Collection = result.Collection,
					SourceFile = result.SourceFile,
					Score = result.Score,
					Title = result.Title,
					Snippet = text2,
					FullContent = (includeFullContent ? text2 : ""),
					LastModified = result.LastModified
				});
			}
			break;
		}
		return list;
	}

	private static string TrimToBudget(string text, int maxChars)
	{
		if (text.Length <= maxChars)
		{
			return text;
		}
		string text2 = text.Substring(0, maxChars);
		int num = text2.LastIndexOfAny(new char[4] { '.', '!', '?', '\n' });
		if (num > maxChars / 2)
		{
			return text2.Substring(0, num + 1).Trim();
		}
		int num2 = text2.LastIndexOf(' ');
		if (num2 > maxChars / 2)
		{
			return text2.Substring(0, num2).Trim() + "...";
		}
		return text2.Trim() + "...";
	}

	private static IReadOnlyList<RetrievalResult> ParseQmdJsonOutput(string json)
	{
		List<RetrievalResult> list = new List<RetrievalResult>();
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in rootElement.EnumerateArray())
				{
					list.Add(new RetrievalResult
					{
						Collection = GetString(item, "collection"),
						SourceFile = (GetString(item, "file") ?? GetString(item, "path") ?? ""),
						Score = GetDouble(item, "score"),
						Title = (GetString(item, "title") ?? ""),
						Snippet = (GetString(item, "snippet") ?? GetString(item, "context") ?? "")
					});
				}
			}
			else if (rootElement.ValueKind == JsonValueKind.Object)
			{
				list.Add(new RetrievalResult
				{
					Collection = GetString(rootElement, "collection"),
					SourceFile = (GetString(rootElement, "file") ?? GetString(rootElement, "path") ?? ""),
					Score = GetDouble(rootElement, "score"),
					Title = (GetString(rootElement, "title") ?? ""),
					Snippet = (GetString(rootElement, "snippet") ?? GetString(rootElement, "context") ?? "")
				});
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private static string? GetString(JsonElement el, string prop)
	{
		JsonElement value;
		return (el.TryGetProperty(prop, out value) && value.ValueKind == JsonValueKind.String) ? value.GetString() : null;
	}

	private static double GetDouble(JsonElement el, string prop)
	{
		JsonElement value;
		double value2;
		return (el.TryGetProperty(prop, out value) && value.TryGetDouble(out value2)) ? value2 : 0.0;
	}

	private static string? FindQmdBinary()
	{
		string[] array = new string[3]
		{
			"/usr/local/bin/qmd",
			"/usr/bin/qmd",
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".npm-global", "bin", "qmd")
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (File.Exists(text))
			{
				return text;
			}
		}
		try
		{
			(string, int) tuple = RunQmd("which", "qmd", 2000);
			if (tuple.Item2 == 0 && !string.IsNullOrWhiteSpace(tuple.Item1))
			{
				return tuple.Item1.Trim();
			}
		}
		catch
		{
		}
		return null;
	}

	private static (string Output, int ExitCode) RunQmd(string args, string? input = null, int timeoutMs = 10000)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = "qmd",
			Arguments = args,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		if (input != null)
		{
			processStartInfo.RedirectStandardInput = true;
		}
		using Process process = Process.Start(processStartInfo);
		if (process == null)
		{
			return (Output: "", ExitCode: 1);
		}
		if (input != null)
		{
			process.StandardInput.Write(input);
			process.StandardInput.Close();
		}
		string item = process.StandardOutput.ReadToEnd();
		process.StandardError.ReadToEnd();
		process.WaitForExit(timeoutMs);
		return (Output: item, ExitCode: process.HasExited ? process.ExitCode : (-1));
	}

	private static async Task<(string Output, int ExitCode)> RunQmdAsync(string args, string? input = null, int timeoutMs = 10000, CancellationToken ct = default(CancellationToken))
	{
		using Process process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "qmd",
			Arguments = args,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		if (input != null)
		{
			process.StartInfo.RedirectStandardInput = true;
		}
		process.Start();
		if (input != null)
		{
			await process.StandardInput.WriteAsync(input);
			process.StandardInput.Close();
		}
		string output = await process.StandardOutput.ReadToEndAsync(ct);
		await process.StandardError.ReadToEndAsync(ct);
		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(timeoutMs);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
		}
		return (Output: output, ExitCode: process.HasExited ? process.ExitCode : (-1));
	}
}
