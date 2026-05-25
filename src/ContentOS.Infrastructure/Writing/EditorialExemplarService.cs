using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Writing;

public interface IEditorialExemplarService
{
    Task<EditorialExemplarContext> BuildContextAsync(string contentType, string primaryKeyword, string summary, CancellationToken cancellationToken = default);
}

public sealed class EditorialExemplarService : IEditorialExemplarService
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EditorialExemplarService> _logger;
    private readonly string _exemplarDir;

    public EditorialExemplarService(IHostEnvironment environment, ILogger<EditorialExemplarService> logger)
    {
        _environment = environment;
        _logger = logger;
        _exemplarDir = Path.Combine(_environment.ContentRootPath, "..", "..", "editorial", "exemplars");
    }

    public async Task<EditorialExemplarContext> BuildContextAsync(string contentType, string primaryKeyword, string summary, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_exemplarDir))
            {
                return EditorialExemplarContext.Empty;
            }

            var files = Directory.GetFiles(_exemplarDir, "*.md")
                .Where(path => !path.EndsWith("README.md", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var tokens = Tokenize($"{contentType} {primaryKeyword} {summary}");
            var ranked = new List<(string Path, string Title, string Content, int Score)>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await File.ReadAllTextAsync(file, cancellationToken);
                if (text.Contains("Status: failed", StringComparison.OrdinalIgnoreCase)) continue;

                var title = text.Split('\n').FirstOrDefault()?.TrimStart('#', ' ').Trim() ?? Path.GetFileNameWithoutExtension(file);
                var score = Score(text, tokens);
                if (score <= 0) continue;
                ranked.Add((file, title, text, score));
            }

            var top = ranked
                .OrderByDescending(x => x.Score)
                .Take(2)
                .ToList();

            if (top.Count == 0)
            {
                return EditorialExemplarContext.Empty;
            }

            var examples = top.Select(x => new EditorialExemplar(
                x.Title,
                BuildPatternSummary(x.Content),
                Path.GetFileName(x.Path))).ToList();

            var patterns = examples
                .SelectMany(x => x.PatternSummary)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            return new EditorialExemplarContext(examples, patterns);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed building editorial exemplar context for {Keyword}", primaryKeyword);
            return EditorialExemplarContext.Empty;
        }
    }

    private static HashSet<string> Tokenize(string input)
    {
        return Regex.Matches(input.ToLowerInvariant(), "[a-z0-9]{3,}")
            .Select(m => m.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int Score(string content, HashSet<string> tokens)
    {
        var lower = content.ToLowerInvariant();
        var score = 0;
        foreach (var token in tokens)
        {
            if (lower.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 2;
        }

        if (lower.Contains("step", StringComparison.OrdinalIgnoreCase)) score += 2;
        if (lower.Contains("budget", StringComparison.OrdinalIgnoreCase)) score += 2;
        if (lower.Contains("save", StringComparison.OrdinalIgnoreCase)) score += 1;
        return score;
    }

    private static IReadOnlyList<string> BuildPatternSummary(string content)
    {
        var patterns = new List<string>();
        var lower = content.ToLowerInvariant();

        if (lower.Contains("step 1") || lower.Contains("step-by-step") || lower.Contains("step by step"))
            patterns.Add("Uses explicit numbered steps early.");
        if (Regex.IsMatch(content, "\\b\\d+%|\\$\\d+|\\b\\d+ steps\\b", RegexOptions.IgnoreCase))
            patterns.Add("Uses concrete numbers or thresholds.");
        if (lower.Contains("for example") || lower.Contains("example") || lower.Contains("if your"))
            patterns.Add("Uses worked examples or scenarios.");
        if (lower.Contains("how to") || lower.Contains("what you") || lower.Contains("quick"))
            patterns.Add("Answers the question quickly and keeps sections scannable.");
        if (lower.Contains("budget") && lower.Contains("income"))
            patterns.Add("Frames advice around real constraints, not ideal conditions.");
        if (patterns.Count == 0)
            patterns.Add("Keeps advice practical and easy to act on.");

        return patterns.Take(4).ToList();
    }
}

public sealed record EditorialExemplarContext(IReadOnlyList<EditorialExemplar> Examples, IReadOnlyList<string> SharedPatterns)
{
    public static EditorialExemplarContext Empty { get; } = new(Array.Empty<EditorialExemplar>(), Array.Empty<string>());
}

public sealed record EditorialExemplar(string Title, IReadOnlyList<string> PatternSummary, string SourceFile);
