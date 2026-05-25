using ContentOS.Domain.Entities;

namespace ContentOS.Infrastructure.Agents;

public interface ILightQaAgent
{
    Task<LightQaResult> RunLightQaCheckAsync(ContentIdea? idea, GeneratedLongformArticle article, CancellationToken cancellationToken = default);
}

public sealed class LightQaAgent : ILightQaAgent
{
    public Task<LightQaResult> RunLightQaCheckAsync(ContentIdea? idea, GeneratedLongformArticle article, CancellationToken cancellationToken = default)
    {
        var issues = new List<LightQaIssue>();

        // --- Grammar checks ---
        var text = article.FullText;

        // Double spaces after periods (common AI artifact)
        var doubleSpaces = System.Text.RegularExpressions.Regex.Matches(text, @"\.\s{2,}[A-Z]");
        if (doubleSpaces.Count > 3)
            issues.Add(new LightQaIssue("Grammar", $"Found {doubleSpaces.Count} instances of double spaces after periods", "Minor"));

        // Repeated words (e.g., "the the")
        var repeatedWords = System.Text.RegularExpressions.Regex.Matches(text, @"\b(\w+)\s+\1\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (repeatedWords.Count > 0)
            issues.Add(new LightQaIssue("Grammar", $"Found {repeatedWords.Count} repeated word(s)", "Minor"));

        // --- Formatting checks ---
        // Empty sections
        var emptySections = article.Sections.Where(s => s.Paragraphs == null || s.Paragraphs.Count == 0).ToList();
        if (emptySections.Count > 0)
            issues.Add(new LightQaIssue("Formatting", $"{emptySections.Count} empty section(s): {string.Join(", ", emptySections.Select(s => s.Heading))}", "Minor"));

        // Very short sections (< 30 chars total)
        var thinSections = article.Sections.Where(s => string.Concat(s.Paragraphs ?? []).Length < 30).ToList();
        if (thinSections.Count > 0)
            issues.Add(new LightQaIssue("Formatting", $"{thinSections.Count} very thin section(s): {string.Join(", ", thinSections.Select(s => s.Heading))}", "Minor"));

        // --- Phrasing checks ---
        // Meta-commentary (should have been caught by strict QA, but double-check)
        var metaPhrases = new[] { "this section should", "the article should", "this guide will", "this post will cover" };
        foreach (var phrase in metaPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                issues.Add(new LightQaIssue("Phrasing", $"Meta-commentary detected: '{phrase}'", "Warning"));
        }

        // Awkward AI phrases
        var awkwardPhrases = new[] { "in today's digital landscape", "it's important to note", "it goes without saying", "at the end of the day", "in this comprehensive guide" };
        foreach (var phrase in awkwardPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                issues.Add(new LightQaIssue("Phrasing", $"Awkward AI phrase: '{phrase}'", "Minor"));
        }

        // --- Result ---
        var passed = issues.Count == 0 || issues.All(i => i.Severity == "Minor");
        var result = new LightQaResult(
            passed ? "Pass" : "Review",
            issues,
            issues.Count == 0 ? "No issues found." : $"{issues.Count} issue(s) flagged: {string.Join("; ", issues.Select(i => i.Description))}",
            passed
        );

        return Task.FromResult(result);
    }
}

public record LightQaResult(string Status, IReadOnlyList<LightQaIssue> Issues, string Summary, bool Passed);

public record LightQaIssue(string Category, string Description, string Severity);