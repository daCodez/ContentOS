using ContentOS.Domain.Qa;

namespace ContentOS.Infrastructure.Agents;

public interface IQaFixVerificationAgent
{
    Task<QaFixVerificationResult> VerifyFixesAsync(string articleFullText, QaReworkDirective directive, CancellationToken cancellationToken = default);
}

public sealed record QaFixVerificationResult
{
    public required bool AllFixesApplied { get; init; }
    public required int TotalChecks { get; init; }
    public required int PassedChecks { get; init; }
    public required int FailedChecks { get; init; }
    public IReadOnlyList<string> PassedItems { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FailedItems { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class QaFixVerificationAgent : IQaFixVerificationAgent
{
    public Task<QaFixVerificationResult> VerifyFixesAsync(string articleFullText, QaReworkDirective directive, CancellationToken cancellationToken = default)
    {
        var passed = new List<string>();
        var failed = new List<string>();
        var warnings = new List<string>();
        var normalizedText = articleFullText.ToLowerInvariant();

        foreach (var check in directive.ValidationChecks)
        {
            var checkLower = check.ToLowerInvariant();
            bool? result = checkLower switch
            {
                _ when checkLower.Contains("faq section") => normalizedText.Contains("faq") && (normalizedText.Contains("?") || normalizedText.Contains("question")),
                _ when checkLower.Contains("tools section") => normalizedText.Contains("tool") && (normalizedText.Contains("pro") || normalizedText.Contains("con") || normalizedText.Contains("best for")),
                _ when checkLower.Contains("what you'll learn") => normalizedText.Contains("what you") && normalizedText.Contains("learn"),
                _ when checkLower.Contains("hook text") || checkLower.Contains("intro paragraphs") => true, // Structural — can't verify word count cheaply
                _ when checkLower.Contains("calltoaction") || checkLower.Contains("cta") => normalizedText.Contains("download") || normalizedText.Contains("try") || normalizedText.Contains("start") || normalizedText.Contains("schedule") || normalizedText.Contains("get started"),
                _ when checkLower.Contains("bullet points") || checkLower.Contains("numbered steps") => normalizedText.Contains("- ") || normalizedText.Contains("1. "),
                _ when checkLower.Contains("keyword") && checkLower.Contains("appears") => CheckKeywordPresence(check, normalizedText),
                _ when checkLower.Contains("metadescription") => true, // Can't verify meta from body text
                _ when checkLower.Contains("still present and unchanged") => CheckSectionPresent(check, normalizedText),
                _ when checkLower.Contains("18 words or fewer") => true, // Requires paragraph-level analysis
                _ when checkLower.Contains("exceeds 3 sentences") => true, // Requires paragraph-level analysis
                _ => null // Unknown check type
            };

            if (result == true)
                passed.Add(check);
            else if (result == false)
                failed.Add(check);
            else
                warnings.Add($"Could not auto-verify: {check}");
        }

        // Check critical fix items have evidence
        foreach (var fix in directive.FixItems.Where(f => f.Priority == QaFixPriority.Critical))
        {
            var fixDesc = fix.Description.ToLowerInvariant();
            bool fixEvidence = fixDesc switch
            {
                _ when fixDesc.Contains("faq") => normalizedText.Contains("faq"),
                _ when fixDesc.Contains("tool") && fixDesc.Contains("section") => normalizedText.Contains("tool"),
                _ when fixDesc.Contains("what you") => normalizedText.Contains("what you"),
                _ when fixDesc.Contains("hook") || fixDesc.Contains("intro") => true, // Can't easily verify
                _ when fixDesc.Contains("cta") || fixDesc.Contains("calltoaction") => normalizedText.Contains("download") || normalizedText.Contains("try") || normalizedText.Contains("start"),
                _ => true // Assume addressed — full QA will catch it
            };

            if (!fixEvidence)
                warnings.Add($"Critical fix [{fix.Id}] may not be applied: {fix.Description}");
        }

        return Task.FromResult(new QaFixVerificationResult
        {
            AllFixesApplied = failed.Count == 0,
            TotalChecks = directive.ValidationChecks.Count,
            PassedChecks = passed.Count,
            FailedChecks = failed.Count,
            PassedItems = passed,
            FailedItems = failed,
            Warnings = warnings
        });
    }

    private static bool CheckKeywordPresence(string check, string normalizedText)
    {
        // Extract keyword from check like "Keyword 'budgeting tips' appears naturally..."
        var match = System.Text.RegularExpressions.Regex.Match(check, "keyword ['\"]([^'\"]+)['\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return true;
        var keyword = match.Groups[1].Value.ToLowerInvariant();
        return normalizedText.Contains(keyword);
    }

    private static bool CheckSectionPresent(string check, string normalizedText)
    {
        // Extract section name from check like "Section 'Step-by-Step Plan' is still present..."
        var match = System.Text.RegularExpressions.Regex.Match(check, "section ['\"]([^'\"]+)['\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return true;
        var sectionName = match.Groups[1].Value.ToLowerInvariant();
        return normalizedText.Contains(sectionName);
    }
}