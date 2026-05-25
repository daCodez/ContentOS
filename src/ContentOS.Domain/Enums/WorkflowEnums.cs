namespace ContentOS.Domain.Enums;

public enum TaskKind
{
    Template,    // Default pipeline step
    Manual,      // User-injected step
    System,      // Auto-inserted (e.g., automatic retry, validation)
    Retry,       // Triggered by a QA fail
    Approval     // Pause for human sign-off
}

public enum TaskStatus
{
    Pending,
    Ready,
    Running,
    InProgress,
    Blocked,
    Completed,
    Failed,
    Rejected,
    Skipped
}

public enum TargetScopeType
{
    Global,     // Entire article
    Section,    // Specific section (via SectionId)
    Metadata,   // Title, Meta, Slug
    Artifact,   // Specific artifact (e.g., SEO package)
    Selection    // Specific text selection
}
