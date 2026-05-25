namespace ContentOS.Domain.Qa;

public sealed record QaReworkDirective
{
    public required int AttemptNumber { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<string> RequiredFixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> HardRuleFailures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SectionsToRewrite { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SectionsToPreserve { get; init; } = Array.Empty<string>();
    public QaRewriteFocus Focus { get; init; } = new();
    public bool RequiresFullRewrite { get; init; }
    public decimal MaxRewriteScopePercent { get; init; } = 100m;
    public string? AdditionalWriterInstructions { get; init; }
    public IReadOnlyList<QaFixItem> FixItems { get; init; } = Array.Empty<QaFixItem>();
    public IReadOnlyList<string> ValidationChecks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForbiddenPatterns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExemplarPatterns { get; init; } = Array.Empty<string>();
    public string? PreReworkSnapshotId { get; init; }
    public string? BeforeAfterSummary { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record QaFixItem
{
    public required string Id { get; init; }
    public required string Section { get; init; }
    public required string Description { get; init; }
    public required QaFixPriority Priority { get; init; }
    public required QaFixCategory Category { get; init; }
}

public enum QaFixPriority
{
    Critical = 0,
    Major = 1,
    Minor = 2
}

public enum QaFixCategory
{
    Structural = 0,
    Seo = 1,
    Readability = 2,
    Monetization = 3,
    Humanization = 4,
    Formatting = 5,
    Hook = 6,
    Cta = 7
}

public sealed record QaRewriteFocus
{
    public IReadOnlyList<string> SeoFixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MonetizationFixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReadabilityFixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> HumanizationFixes { get; init; } = Array.Empty<string>();
    public string? ReplacementHookGuidance { get; init; }
    public string? ReplacementCtaGuidance { get; init; }
}