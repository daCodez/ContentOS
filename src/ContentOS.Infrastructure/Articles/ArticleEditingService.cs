using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Articles;

public interface IArticleEditingService
{
    Task<ArticleEditingSessionDto> GetSessionAsync(Guid articleIdOrWorkflowJobId, CancellationToken cancellationToken = default);
    Task<ArticleEditRequestDto> CreateEditProposalAsync(Guid articleIdOrWorkflowJobId, string message, string scope, string? targetSectionId, string? selectedText, CancellationToken cancellationToken = default);
    Task AcceptEditAsync(Guid articleIdOrWorkflowJobId, Guid requestId, string appliedBy, CancellationToken cancellationToken = default);
    Task RejectEditAsync(Guid articleIdOrWorkflowJobId, Guid requestId, string rejectedBy, CancellationToken cancellationToken = default);
    Task<ArticleFieldLocksDto> UpdateFieldLocksAsync(Guid articleIdOrWorkflowJobId, ArticleFieldLocksDto locks, CancellationToken cancellationToken = default);
}

public sealed class ArticleEditingService : IArticleEditingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<ArticleEditingService> _logger;
    private readonly IReadOnlyDictionary<string, IArticleEditRunner> _runners;

    public ArticleEditingService(ContentOsDbContext dbContext, ILogger<ArticleEditingService> logger, IEnumerable<IArticleEditRunner> runners)
    {
        _dbContext = dbContext;
        _logger = logger;
        _runners = runners.ToDictionary(x => x.AgentName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ArticleEditingSessionDto> GetSessionAsync(Guid articleIdOrWorkflowJobId, CancellationToken cancellationToken = default)
    {
        var article = await EnsureArticleAsync(articleIdOrWorkflowJobId, cancellationToken);
        var thread = await GetOrCreateThreadAsync(article, cancellationToken);

        var messages = await _dbContext.ArticleChatMessages
            .Where(x => x.ThreadId == thread.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var requests = await _dbContext.ArticleEditRequests
            .Include(x => x.Patches)
            .Where(x => x.ArticleId == article.Id && x.Status == "Pending")
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var revisions = await _dbContext.ArticleRevisions
            .Where(x => x.ArticleId == article.Id)
            .OrderByDescending(x => x.RevisionNumber)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new ArticleEditingSessionDto
        {
            ArticleId = article.Id,
            ThreadId = thread.Id,
            Messages = messages.Select(MapMessage).ToList(),
            PendingRequests = requests.Select(MapRequest).ToList(),
            Revisions = revisions.Select(MapRevision).ToList(),
            FieldLocks = ReadFieldLocks(thread.Title)
        };
    }

    public async Task<ArticleEditRequestDto> CreateEditProposalAsync(Guid articleIdOrWorkflowJobId, string message, string scope, string? targetSectionId, string? selectedText, CancellationToken cancellationToken = default)
    {
        var article = await EnsureArticleAsync(articleIdOrWorkflowJobId, cancellationToken);
        var thread = await GetOrCreateThreadAsync(article, cancellationToken);
        var normalizedScope = NormalizeScope(scope);
        var intent = ClassifyIntent(message);
        var routedAgent = RouteAgent(intent);
        var target = ResolveTarget(article, normalizedScope, targetSectionId, selectedText);
        var fieldLocks = ReadFieldLocks(thread.Title);
        var proposal = await BuildProposalAsync(article, message, intent, normalizedScope, routedAgent, target, fieldLocks, cancellationToken);

        var request = new ArticleEditRequest
        {
            Id = Guid.NewGuid(),
            ArticleId = article.Id,
            ThreadId = thread.Id,
            UserMessage = message.Trim(),
            Intent = intent,
            Scope = normalizedScope,
            TargetSectionId = target.SectionId,
            TargetLabel = target.Label,
            SelectedText = selectedText?.Trim() ?? string.Empty,
            Status = "Pending",
            RoutedAgent = routedAgent,
            QaStatus = proposal.QaStatus,
            QaSummary = proposal.QaSummary,
            DiffSummary = proposal.DiffSummary,
            ProposedTitle = proposal.Title,
            ProposedSummary = proposal.Summary,
            ProposedContent = proposal.Content,
            ProposedMetaDescription = proposal.MetaDescription,
            ProposedCallToAction = proposal.CallToAction,
            ProposalJson = JsonSerializer.Serialize(proposal, JsonOptions),
            LockedFields = SerializeLocks(fieldLocks),
            CreatedAtUtc = DateTime.UtcNow
        };

        request.Patches.Add(new ArticleEditPatch
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            TargetType = normalizedScope,
            TargetId = target.SectionId,
            Operation = proposal.Operation,
            BeforeContent = target.OriginalContent,
            ProposedContent = target.ProposedContent,
            Rationale = proposal.Rationale,
            Warnings = string.Join(" | ", proposal.Warnings),
            FieldLocksRespected = true,
            SortOrder = 1
        });

        var userMessage = new ArticleChatMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            RequestId = request.Id,
            Role = "user",
            Content = message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var assistantMessage = new ArticleChatMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            RequestId = request.Id,
            Role = "assistant",
            Content = BuildAssistantSummary(request),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ArticleEditRequests.Add(request);
        _dbContext.ArticleChatMessages.AddRange(userMessage, assistantMessage);
        thread.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created article edit proposal {RequestId} for article {ArticleId} via {Agent}", request.Id, article.Id, routedAgent);

        return MapRequest(request);
    }

    public async Task AcceptEditAsync(Guid articleIdOrWorkflowJobId, Guid requestId, string appliedBy, CancellationToken cancellationToken = default)
    {
        var article = await EnsureArticleAsync(articleIdOrWorkflowJobId, cancellationToken);
        var request = await _dbContext.ArticleEditRequests
            .Include(x => x.Patches)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.ArticleId == article.Id, cancellationToken)
            ?? throw new InvalidOperationException("Edit request not found.");

        if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending edits can be accepted.");
        }

        article.Title = request.ProposedTitle;
        article.Summary = request.ProposedSummary;
        article.Content = request.ProposedContent;
        article.MetaDescription = request.ProposedMetaDescription;
        article.CallToAction = request.ProposedCallToAction;
        article.UpdatedAt = DateTime.UtcNow;

        var nextRevision = await _dbContext.ArticleRevisions
            .Where(x => x.ArticleId == article.Id)
            .Select(x => (int?)x.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var revision = new ArticleRevision
        {
            Id = Guid.NewGuid(),
            ArticleId = article.Id,
            RequestId = request.Id,
            RevisionNumber = nextRevision + 1,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            MetaDescription = article.MetaDescription,
            CallToAction = article.CallToAction,
            DiffSummary = request.DiffSummary,
            ChangedBy = appliedBy,
            CreatedAtUtc = DateTime.UtcNow
        };

        request.Status = "Accepted";
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.AppliedAtUtc = DateTime.UtcNow;

        var thread = await _dbContext.ArticleChatThreads.FirstAsync(x => x.Id == request.ThreadId, cancellationToken);
        thread.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.ArticleRevisions.Add(revision);
        _dbContext.ArticleChatMessages.Add(new ArticleChatMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            RequestId = request.Id,
            Role = "assistant",
            Content = $"Applied revision {revision.RevisionNumber}. {request.DiffSummary}",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArticleFieldLocksDto> UpdateFieldLocksAsync(Guid articleIdOrWorkflowJobId, ArticleFieldLocksDto locks, CancellationToken cancellationToken = default)
    {
        var article = await EnsureArticleAsync(articleIdOrWorkflowJobId, cancellationToken);
        var thread = await GetOrCreateThreadAsync(article, cancellationToken);
        thread.Title = SaveLocksToThreadTitle(article.Title, locks);
        thread.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return locks;
    }

    public async Task RejectEditAsync(Guid articleIdOrWorkflowJobId, Guid requestId, string rejectedBy, CancellationToken cancellationToken = default)
    {
        var article = await EnsureArticleAsync(articleIdOrWorkflowJobId, cancellationToken);
        var request = await _dbContext.ArticleEditRequests
            .FirstOrDefaultAsync(x => x.Id == requestId && x.ArticleId == article.Id, cancellationToken)
            ?? throw new InvalidOperationException("Edit request not found.");

        if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending edits can be rejected.");
        }

        request.Status = "Rejected";
        request.ReviewedAtUtc = DateTime.UtcNow;

        _dbContext.ArticleChatMessages.Add(new ArticleChatMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = request.ThreadId,
            RequestId = request.Id,
            Role = "assistant",
            Content = $"Rejected the proposed edit. No article content was changed. ({rejectedBy})",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Article> EnsureArticleAsync(Guid articleIdOrWorkflowJobId, CancellationToken cancellationToken)
    {
        var article = await _dbContext.Articles.FirstOrDefaultAsync(x => x.Id == articleIdOrWorkflowJobId, cancellationToken);
        if (article is not null) return article;

        var artifact = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == articleIdOrWorkflowJobId && (x.ArtifactType == "FinalArticlePreview" || x.ArtifactType == "PublishReadyPackage"))
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (artifact is null)
        {
            throw new InvalidOperationException($"Article not found for id {articleIdOrWorkflowJobId}.");
        }

        var payload = ParseJson(artifact.ContentJson);
        var title = GetJsonString(payload, "title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            var existingArticle = await _dbContext.Articles
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(x => x.Title == title, cancellationToken);
            if (existingArticle is not null) return existingArticle;
        }

        var articleEntity = new Article
        {
            Id = articleIdOrWorkflowJobId,
            Title = GetJsonString(payload, "title"),
            Summary = GetJsonString(payload, "summary"),
            Content = GetJsonString(payload, "htmlBody"),
            MetaDescription = GetJsonString(payload, "metaDescription"),
            CallToAction = GetJsonString(payload, "callToAction"),
            Author = "Workflow",
            CreatedAt = artifact.CreatedUtc,
            UpdatedAt = DateTime.UtcNow,
            Status = "Generated",
            IsPublished = true
        };

        if (string.IsNullOrWhiteSpace(articleEntity.Content))
        {
            articleEntity.Content = BuildHtmlBody(payload);
        }

        _dbContext.Articles.Add(articleEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return articleEntity;
    }

    private async Task<ArticleChatThread> GetOrCreateThreadAsync(Article article, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ArticleChatThreads
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(x => x.ArticleId == article.Id, cancellationToken);

        if (existing is not null) return existing;

        var thread = new ArticleChatThread
        {
            Id = Guid.NewGuid(),
            ArticleId = article.Id,
            Title = $"Edit {article.Title}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ArticleChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return thread;
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = scope?.Trim().ToLowerInvariant();
        return normalized is "selection" or "section" or "article" ? normalized : "section";
    }

    private static string ClassifyIntent(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        if (lower.Contains("seo") || lower.Contains("keyword") || lower.Contains("meta") || lower.Contains("internal link")) return "seo";
        if (lower.Contains("cta") || lower.Contains("affiliate") || lower.Contains("convert")) return "monetization";
        if (lower.Contains("bullet") || lower.Contains("skim") || lower.Contains("format")) return "formatting";
        if (lower.Contains("brand") || lower.Contains("tone") || lower.Contains("warm") || lower.Contains("robotic")) return "tone";
        if (lower.Contains("fact") || lower.Contains("research") || lower.Contains("source")) return "fact";
        return "writing";
    }

    private static string RouteAgent(string intent)
        => intent switch
        {
            "seo" => "SeoOptimizerAgent",
            "monetization" => "MonetizationAgent",
            "tone" => "HumanizerAgent",
            "formatting" => "WriterAgent",
            "fact" => "QaAgent",
            _ => "WriterAgent"
        };

    private static EditTarget ResolveTarget(Article article, string scope, string? targetSectionId, string? selectedText)
    {
        var content = article.Content ?? string.Empty;
        if (scope == "selection" && !string.IsNullOrWhiteSpace(selectedText))
        {
            return new EditTarget("selection", targetSectionId ?? "selection", "Selected text", selectedText.Trim(), selectedText.Trim());
        }

        if (scope == "section")
        {
            var sections = ExtractSections(content).ToList();
            var matched = sections.FirstOrDefault(x => string.Equals(x.SectionId, targetSectionId, StringComparison.OrdinalIgnoreCase))
                ?? sections.FirstOrDefault()
                ?? new ArticleSection("article-body", "Article body", content);
            return new EditTarget("section", matched.SectionId, matched.Heading, matched.Html, matched.Html);
        }

        return new EditTarget("article", "article", "Whole article", content, content);
    }

    private async Task<EditProposal> BuildProposalAsync(Article article, string message, string intent, string scope, string agent, EditTarget target, ArticleFieldLocksDto fieldLocks, CancellationToken cancellationToken)
    {
        var runner = ResolveRunner(agent);
        var result = await runner.RunAsync(new ArticleEditRunnerContext(
            article.Id,
            intent,
            scope,
            message,
            target.Label,
            target.OriginalContent,
            article.Title,
            article.Summary,
            article.MetaDescription,
            article.CallToAction,
            fieldLocks), cancellationToken);

        var proposedTitle = result.ProposedTitle ?? article.Title;
        var proposedSummary = result.ProposedSummary ?? article.Summary;
        var proposedContent = article.Content;
        var proposedMeta = result.ProposedMetaDescription ?? article.MetaDescription;
        var proposedCta = result.ProposedCallToAction ?? article.CallToAction;
        var warnings = result.Warnings.ToList();
        var updatedTargetContent = result.ProposedContent;

        if (scope == "section" || scope == "selection")
        {
            proposedContent = ReplaceTarget(article.Content, target, updatedTargetContent);
        }
        else if (result.Operation != "update-field")
        {
            proposedContent = updatedTargetContent;
        }

        ApplyLocks(fieldLocks, article, ref proposedTitle, ref proposedSummary, ref proposedMeta, ref proposedCta, warnings);

        var qa = RunQa(target.OriginalContent, updatedTargetContent, result.Operation, warnings);
        var diffSummary = BuildDiffSummary(target.Label, target.OriginalContent, updatedTargetContent, proposedMeta, article.MetaDescription, proposedCta, article.CallToAction);

        return new EditProposal(
            proposedTitle,
            proposedSummary,
            proposedContent,
            proposedMeta,
            proposedCta,
            qa.Status,
            qa.Summary,
            diffSummary,
            result.Operation,
            result.Rationale,
            warnings.Append(result.ModelNotes).ToList(),
            target.Label,
            target.SectionId,
            updatedTargetContent);
    }

    private static QaCheck RunQa(string beforeContent, string afterContent, string operation, List<string> warnings)
    {
        var issues = new List<string>(warnings);
        if (operation == "review") issues.Add("Manual verification recommended before apply.");
        if (CountWords(StripHtml(afterContent)) < Math.Max(10, CountWords(StripHtml(beforeContent)) / 4) && operation != "update-field")
        {
            issues.Add("Proposal removed too much content for a narrow edit.");
        }
        if (Regex.IsMatch(afterContent, "in this comprehensive guide|it is important to note", RegexOptions.IgnoreCase))
        {
            issues.Add("AI-style filler phrase detected.");
        }

        return issues.Count == 0
            ? new QaCheck("Pass", "No blocking issues found.")
            : new QaCheck(issues.Any(x => x.Contains("removed too much", StringComparison.OrdinalIgnoreCase)) ? "Review" : "Pass with notes", string.Join(" ", issues));
    }

    private static string BuildAssistantSummary(ArticleEditRequest request)
        => $"Proposed a {request.Scope} edit via {request.RoutedAgent}. {request.DiffSummary} QA: {request.QaStatus}.";

    private static string ReplaceTarget(string html, EditTarget target, string updatedTargetContent)
    {
        if (target.Scope == "selection" && !string.IsNullOrWhiteSpace(target.OriginalContent))
        {
            return html.Replace(target.OriginalContent, updatedTargetContent, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(target.OriginalContent) && html.Contains(target.OriginalContent, StringComparison.Ordinal))
        {
            return html.Replace(target.OriginalContent, updatedTargetContent, StringComparison.Ordinal);
        }

        return html;
    }

    private static IEnumerable<ArticleSection> ExtractSections(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) yield break;

        var matches = Regex.Matches(html, "<section[^>]*>(.*?)</section>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var sectionHtml = match.Value;
            var headingMatch = Regex.Match(sectionHtml, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var heading = headingMatch.Success ? StripHtml(headingMatch.Groups[1].Value) : "Section";
            var sectionId = Slugify(heading);
            yield return new ArticleSection(sectionId, heading, sectionHtml);
        }

        if (matches.Count == 0)
        {
            yield return new ArticleSection("article-body", "Article body", html);
        }
    }

    private static string MakeWarmerAndLessRobotic(string html)
    {
        var updated = html;
        updated = Regex.Replace(updated, "\bIn conclusion,?\b", "To wrap up,", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bIt is important to note that\b", "A useful thing to remember is", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bAdditionally,\b", "Also,", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bUtilize\b", "Use", RegexOptions.IgnoreCase);
        return updated;
    }

    private static string MakeMoreSkimmable(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;
        var paragraphs = Regex.Matches(html, "<p>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(x => x.Groups[1].Value)
            .ToList();

        if (paragraphs.Count == 0) return html;

        var builder = new StringBuilder();
        var first = paragraphs[0];
        builder.Append("<p>").Append(first).Append("</p>");

        var bulletItems = paragraphs.Skip(1)
            .Select(p => StripHtml(p).Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Take(4)
            .ToList();

        if (bulletItems.Count >= 2)
        {
            builder.Append("<ul>");
            foreach (var item in bulletItems)
            {
                builder.Append("<li>").Append(System.Net.WebUtility.HtmlEncode(item)).Append("</li>");
            }
            builder.Append("</ul>");
        }
        else
        {
            builder.Append(string.Join(string.Empty, paragraphs.Skip(1).Select(p => $"<p>{p}</p>")));
        }

        return builder.ToString();
    }

    private static string AddSeoScaffolding(string html, string title)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;
        if (Regex.IsMatch(html, "<strong>Key takeaway:</strong>", RegexOptions.IgnoreCase)) return html;
        return $"<p><strong>Key takeaway:</strong> {System.Net.WebUtility.HtmlEncode(title)} works best when the advice is practical, clear, and easy to apply.</p>{html}";
    }

    private static string RewriteForClarity(string html)
    {
        var updated = html;
        updated = Regex.Replace(updated, "\bHowever,\b", "But", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bTherefore,\b", "So", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, "\bIn order to\b", "To", RegexOptions.IgnoreCase);
        return updated;
    }

    private IArticleEditRunner ResolveRunner(string agent)
        => _runners.TryGetValue(agent, out var runner)
            ? runner
            : _runners["WriterAgent"];

    private static string ImproveTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return title;
        if (title.Contains(":", StringComparison.Ordinal)) return title;
        return title.Length < 55 ? $"{title}: A Practical Guide" : title;
    }

    private static void ApplyLocks(ArticleFieldLocksDto locks, Article article, ref string proposedTitle, ref string proposedSummary, ref string proposedMeta, ref string proposedCta, List<string> warnings)
    {
        if (locks.TitleLocked)
        {
            proposedTitle = article.Title;
            warnings.Add("Title lock respected.");
        }

        if (locks.SummaryLocked)
        {
            proposedSummary = article.Summary;
            warnings.Add("Summary lock respected.");
        }

        if (locks.MetaDescriptionLocked)
        {
            proposedMeta = article.MetaDescription;
            warnings.Add("Meta description lock respected.");
        }

        if (locks.CallToActionLocked)
        {
            proposedCta = article.CallToAction;
            warnings.Add("CTA lock respected.");
        }
    }

    private static ArticleFieldLocksDto ReadFieldLocks(string threadTitle)
    {
        var markerIndex = threadTitle.IndexOf("::locks=", StringComparison.Ordinal);
        if (markerIndex < 0) return new ArticleFieldLocksDto();
        var json = threadTitle[(markerIndex + 8)..];
        try
        {
            return JsonSerializer.Deserialize<ArticleFieldLocksDto>(json, JsonOptions) ?? new ArticleFieldLocksDto();
        }
        catch
        {
            return new ArticleFieldLocksDto();
        }
    }

    private static string SaveLocksToThreadTitle(string articleTitle, ArticleFieldLocksDto locks)
        => $"Edit {articleTitle}::locks={SerializeLocks(locks)}";

    private static string SerializeLocks(ArticleFieldLocksDto locks)
        => JsonSerializer.Serialize(locks, JsonOptions);

    private static string BuildMetaDescription(string title, string summary)
    {
        var seed = string.IsNullOrWhiteSpace(summary) ? title : summary;
        var plain = StripHtml(seed).Trim();
        if (plain.Length > 150) plain = plain[..150].TrimEnd();
        return plain;
    }

    private static string SummarizeHtml(string html, string fallback)
    {
        var plain = StripHtml(html);
        if (plain.Length < 40) return fallback;
        return plain.Length > 180 ? plain[..180].TrimEnd() + "..." : plain;
    }

    private static string BuildDiffSummary(string targetLabel, string before, string after, string proposedMeta, string currentMeta, string proposedCta, string currentCta)
    {
        var beforeWords = CountWords(StripHtml(before));
        var afterWords = CountWords(StripHtml(after));
        var changes = new List<string>
        {
            $"Updated {targetLabel.ToLowerInvariant()} ({beforeWords} → {afterWords} words)"
        };

        if (!string.Equals(proposedMeta, currentMeta, StringComparison.Ordinal)) changes.Add("meta description changed");
        if (!string.Equals(proposedCta, currentCta, StringComparison.Ordinal)) changes.Add("CTA changed");
        return string.Join(", ", changes) + ".";
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static string StripHtml(string html)
        => Regex.Replace(System.Net.WebUtility.HtmlDecode(html ?? string.Empty), "<[^>]+>", " ").Replace("\n", " ").Trim();

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "section" : slug;
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static string GetJsonString(JsonElement? element, string propertyName)
    {
        if (element is null) return string.Empty;
        if (!TryGetProperty(element.Value, propertyName, out var property)) return string.Empty;
        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
    }

    private static string BuildHtmlBody(JsonElement? payload)
    {
        if (payload is null) return string.Empty;
        var blocks = new List<string>();
        blocks.AddRange(GetArticleParagraphs(payload, "introParagraphs", "intro").Select(RenderHtmlBlock));
        foreach (var section in GetFinalArticleSections(payload))
        {
            blocks.Add($"<section class=\"article-read-section\"><h2>{System.Net.WebUtility.HtmlEncode(section.Heading)}</h2>{string.Join(string.Empty, section.Paragraphs.Select(RenderHtmlBlock))}</section>");
        }
        var conclusion = GetArticleParagraphs(payload, "conclusionParagraphs", "conclusion").ToList();
        if (conclusion.Any())
        {
            blocks.Add($"<section class=\"article-read-section\"><h2>Conclusion</h2>{string.Join(string.Empty, conclusion.Select(RenderHtmlBlock))}</section>");
        }
        return string.Join("\n", blocks);
    }

    private static IEnumerable<string> GetArticleParagraphs(JsonElement? payload, string arrayPropertyName, string fallbackPropertyName)
    {
        if (payload is null) return Enumerable.Empty<string>();
        if (TryGetProperty(payload.Value, arrayPropertyName, out var arrayProperty) && arrayProperty.ValueKind == JsonValueKind.Array)
        {
            return arrayProperty.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }
        var fallback = GetJsonString(payload, fallbackPropertyName);
        return string.IsNullOrWhiteSpace(fallback)
            ? Enumerable.Empty<string>()
            : fallback.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<(string Heading, IEnumerable<string> Paragraphs)> GetFinalArticleSections(JsonElement? payload)
    {
        if (payload is null) return Enumerable.Empty<(string Heading, IEnumerable<string> Paragraphs)>();
        if (TryGetProperty(payload.Value, "sections", out var sectionsProperty) && sectionsProperty.ValueKind == JsonValueKind.Array)
        {
            return sectionsProperty.EnumerateArray()
                .Select(section =>
                {
                    var heading = TryGetProperty(section, "heading", out var headingProperty)
                        ? headingProperty.GetString() ?? string.Empty
                        : string.Empty;

                    var paragraphs = TryGetProperty(section, "paragraphs", out var paragraphsProperty) && paragraphsProperty.ValueKind == JsonValueKind.Array
                        ? paragraphsProperty.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString()).Where(item => !string.IsNullOrWhiteSpace(item))
                        : Enumerable.Empty<string>();

                    return (Heading: heading, Paragraphs: paragraphs);
                })
                .Where(section => !string.IsNullOrWhiteSpace(section.Heading) && section.Paragraphs.Any());
        }
        return Enumerable.Empty<(string Heading, IEnumerable<string> Paragraphs)>();
    }

    private static bool TryGetProperty(JsonElement payload, string propertyName, out JsonElement property)
    {
        if (payload.TryGetProperty(propertyName, out property)) return true;
        foreach (var candidate in payload.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }
        property = default;
        return false;
    }

    private static string RenderHtmlBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0 && lines.All(line => line.StartsWith("- ", StringComparison.Ordinal)))
        {
            return "<ul>" + string.Join(string.Empty, lines.Select(line => $"<li>{System.Net.WebUtility.HtmlEncode(line[2..])}</li>")) + "</ul>";
        }
        return $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>";
    }

    private static ArticleChatMessageDto MapMessage(ArticleChatMessage message)
        => new()
        {
            Id = message.Id,
            Role = message.Role,
            Content = message.Content,
            CreatedAtUtc = message.CreatedAtUtc
        };

    private static ArticleEditRequestDto MapRequest(ArticleEditRequest request)
        => new()
        {
            Id = request.Id,
            ThreadId = request.ThreadId,
            UserMessage = request.UserMessage,
            Intent = request.Intent,
            Scope = request.Scope,
            TargetSectionId = request.TargetSectionId,
            TargetLabel = request.TargetLabel,
            SelectedText = request.SelectedText,
            Status = request.Status,
            RoutedAgent = request.RoutedAgent,
            QaStatus = request.QaStatus,
            QaSummary = request.QaSummary,
            DiffSummary = request.DiffSummary,
            ProposedTitle = request.ProposedTitle,
            ProposedSummary = request.ProposedSummary,
            ProposedContent = request.ProposedContent,
            ProposedMetaDescription = request.ProposedMetaDescription,
            ProposedCallToAction = request.ProposedCallToAction,
            LockedFields = request.LockedFields,
            CreatedAtUtc = request.CreatedAtUtc,
            Patches = request.Patches.OrderBy(x => x.SortOrder).Select(x => new ArticleEditPatchDto
            {
                Id = x.Id,
                TargetType = x.TargetType,
                TargetId = x.TargetId,
                Operation = x.Operation,
                BeforeContent = x.BeforeContent,
                ProposedContent = x.ProposedContent,
                Rationale = x.Rationale,
                Warnings = x.Warnings,
                FieldLocksRespected = x.FieldLocksRespected,
                SortOrder = x.SortOrder
            }).ToList()
        };

    private static ArticleRevisionDto MapRevision(ArticleRevision revision)
        => new()
        {
            Id = revision.Id,
            RevisionNumber = revision.RevisionNumber,
            RequestId = revision.RequestId,
            Title = revision.Title,
            Summary = revision.Summary,
            Content = revision.Content,
            MetaDescription = revision.MetaDescription,
            CallToAction = revision.CallToAction,
            DiffSummary = revision.DiffSummary,
            ChangedBy = revision.ChangedBy,
            CreatedAtUtc = revision.CreatedAtUtc
        };

    private sealed record EditTarget(string Scope, string SectionId, string Label, string OriginalContent, string ProposedContent);
    private sealed record QaCheck(string Status, string Summary);
    private sealed record EditProposal(
        string Title,
        string Summary,
        string Content,
        string MetaDescription,
        string CallToAction,
        string QaStatus,
        string QaSummary,
        string DiffSummary,
        string Operation,
        string Rationale,
        IReadOnlyList<string> Warnings,
        string TargetLabel,
        string TargetSectionId,
        string ProposedTargetContent);

    private sealed record ArticleSection(string SectionId, string Heading, string Html);
}
