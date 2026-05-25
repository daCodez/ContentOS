using System;
using System.Collections.Generic;

namespace ContentOS.Domain.Entities;

public class ArticleEditRequest
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid ThreadId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string TargetSectionId { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
    public string SelectedText { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RoutedAgent { get; set; } = string.Empty;
    public string QaStatus { get; set; } = string.Empty;
    public string QaSummary { get; set; } = string.Empty;
    public string DiffSummary { get; set; } = string.Empty;
    public string ProposedTitle { get; set; } = string.Empty;
    public string ProposedSummary { get; set; } = string.Empty;
    public string ProposedContent { get; set; } = string.Empty;
    public string ProposedMetaDescription { get; set; } = string.Empty;
    public string ProposedCallToAction { get; set; } = string.Empty;
    public string ProposalJson { get; set; } = string.Empty;
    public string LockedFields { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }

    public virtual ArticleChatThread Thread { get; set; } = null!;
    public virtual ICollection<ArticleEditPatch> Patches { get; set; } = new List<ArticleEditPatch>();
}
