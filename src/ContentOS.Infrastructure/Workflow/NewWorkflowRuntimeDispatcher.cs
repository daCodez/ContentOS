using System.Text.Json;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Research;
using ContentOS.Application.Research;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Infrastructure.Workflow;

/// <summary>Bridges approved idea snapshots to article capabilities while enforcing generation and QA failures.</summary>
public sealed class NewWorkflowRuntimeDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContentOsDbContext _dbContext;

    public NewWorkflowRuntimeDispatcher(IServiceScopeFactory scopeFactory, ContentOsDbContext dbContext)
    {
        _scopeFactory = scopeFactory;
        _dbContext = dbContext;
    }

    /// <summary>Executes an article capability and enforces its returned quality decision.</summary>
    /// <remarks>Rejected QA is a failed action, not a successfully produced report. The runtime must not promote the next action.</remarks>
    /// <param name="workflowRun">The approved article workflow.</param>
    /// <param name="actionRun">The action instance being executed.</param>
    /// <param name="actionDefinition">The configured capability.</param>
    /// <param name="cancellationToken">Cancels database and agent work.</param>
    /// <returns>The payload and article for a successful capability.</returns>
    /// <exception cref="InvalidOperationException">The approved idea is missing or QA rejects the article.</exception>
    public async Task<NewWorkflowDispatchResult> DispatchAsync(
        WorkflowDefinitionRun workflowRun,
        WorkflowActionRun actionRun,
        WorkflowActionDefinition actionDefinition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actionDefinition.CapabilityKey is not ("DraftArticle" or "HumanizeArticle" or "DeoptimizeArticle" or "PreQaSeoValidation" or "QaScoring"))
            throw new InvalidOperationException($"Unsupported article capability '{actionDefinition.CapabilityKey}'. Configure an implemented capability before retrying.");
        using var scope = _scopeFactory.CreateScope();
        var coordinator = (WorkflowCoordinatorAgent)scope.ServiceProvider.GetRequiredService<IWorkflowCoordinatorAgent>();

        var idea = workflowRun.IdeaRecordId.HasValue
            ? await _dbContext.IdeaRecords.FirstOrDefaultAsync(x => x.Id == workflowRun.IdeaRecordId.Value, cancellationToken)
            : null;

        if (idea is null)
        {
            throw new InvalidOperationException("Article workflow requires an approved idea snapshot.");
        }

        var article = await BuildArticleAsync(workflowRun, idea, cancellationToken);
        var legacyTask = BuildCompatTask(workflowRun, actionRun, actionDefinition);
        var legacyIdea = BuildCompatIdea(idea);

        // Older approved snapshots only stored the legacy ID. Read its research fields
        // without replacing the approved title, reader problem, intent or angle.
        using (var snapshot = JsonDocument.Parse(string.IsNullOrWhiteSpace(idea.IdeaSnapshotJson) ? "{}" : idea.IdeaSnapshotJson))
        {
            if (!snapshot.RootElement.TryGetProperty("primaryKeyword", out _)
                && !snapshot.RootElement.TryGetProperty("PrimaryKeyword", out _) && legacyIdea.Id != Guid.Empty)
            {
                var researchIdea = await _dbContext.ContentIdeas.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == legacyIdea.Id, cancellationToken);
                if (researchIdea is not null)
                {
                    legacyIdea.PrimaryKeyword = researchIdea.PrimaryKeyword;
                    legacyIdea.SecondaryKeywordsJson = researchIdea.SecondaryKeywordsJson;
                    legacyIdea.SourceSummaryJson = researchIdea.SourceSummaryJson;
                    legacyIdea.Summary = researchIdea.Summary;
                    legacyIdea.ContentType = researchIdea.ContentType;
                    legacyIdea.SlugSuggestion = researchIdea.SlugSuggestion;
                }
            }
        }

        object payload = actionDefinition.CapabilityKey switch
        {
            "DraftArticle" => await BuildAndWriteDraftAsync(coordinator, workflowRun, actionRun, actionDefinition, idea, article, legacyTask, legacyIdea, cancellationToken),
            "HumanizeArticle" => await coordinator.BuildHumanizedPayloadAsync(legacyTask, legacyIdea, article, cancellationToken),
            "DeoptimizeArticle" => await coordinator.BuildHumanizedPayloadAsync(legacyTask, legacyIdea, article, cancellationToken),
            "PreQaSeoValidation" => await coordinator.BuildStrictQaPayloadAsync(legacyTask, legacyIdea, article, cancellationToken),
            "QaScoring" => await coordinator.BuildLightQaPayloadAsync(legacyTask, legacyIdea, article, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported article capability '{actionDefinition.CapabilityKey}'.")
        };

        if (payload is QaReportResult qa && !qa.IsQualitySufficient)
            throw new InvalidOperationException($"Article QA blocked: {string.Join("; ", qa.HardRuleFailures)}");
        if (payload is LightQaResult lightQa && !lightQa.Passed)
            throw new InvalidOperationException($"Article QA blocked: {lightQa.Summary}");

        return new NewWorkflowDispatchResult(payload, article);
    }

    private async Task<GeneratedLongformArticle> BuildArticleAsync(WorkflowDefinitionRun workflowRun, IdeaRecord idea, CancellationToken cancellationToken)
    {
        var completedRuns = await _dbContext.WorkflowActionRuns
            .Where(x => x.WorkflowDefinitionRunId == workflowRun.Id && x.Status == Domain.Enums.WorkflowDefinitionRunStatus.Completed)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        var title = idea.IdeaTitle;
        var summary = idea.ReaderProblem;
        var metaDescription = idea.ReaderProblem;
        var bodyText = string.Empty;
        var introParagraphs = new List<string>();
        var sections = new List<GeneratedSection>();
        var conclusionParagraphs = new List<string>();
        var callToAction = string.Empty;
        var slug = string.Empty;
        var targetWordCountMin = 1800;
        var targetWordCountMax = 2600;

        // Also check ContentArtifacts for the richest version of the article
        var latestArtifact = await _dbContext.ContentArtifacts
            .Where(a => a.ContentWorkflowJobId == workflowRun.Id)
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestArtifact is not null && !string.IsNullOrWhiteSpace(latestArtifact.ContentJson))
        {
            try
            {
                using var artifactDoc = JsonDocument.Parse(latestArtifact.ContentJson);
                var root = artifactDoc.RootElement;

                if (root.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String)
                    title = t.GetString() ?? title;
                if (root.TryGetProperty("Slug", out var sl) && sl.ValueKind == JsonValueKind.String)
                    slug = sl.GetString() ?? slug;
                if (root.TryGetProperty("Summary", out var su) && su.ValueKind == JsonValueKind.String)
                    summary = su.GetString() ?? summary;
                if (root.TryGetProperty("MetaDescription", out var md) && md.ValueKind == JsonValueKind.String)
                    metaDescription = md.GetString() ?? metaDescription;
                if (root.TryGetProperty("TargetWordCountMin", out var twmin) && twmin.ValueKind == JsonValueKind.Number)
                    targetWordCountMin = twmin.GetInt32();
                if (root.TryGetProperty("TargetWordCountMax", out var twmax) && twmax.ValueKind == JsonValueKind.Number)
                    targetWordCountMax = twmax.GetInt32();
                if (root.TryGetProperty("IntroParagraphs", out var ip) && ip.ValueKind == JsonValueKind.Array)
                    introParagraphs = ip.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? "").ToList();
                if (root.TryGetProperty("Sections", out var sec) && sec.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sec.EnumerateArray())
                    {
                        var heading = s.TryGetProperty("Heading", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() ?? "" : "";
                        var paras = s.TryGetProperty("Paragraphs", out var p) && p.ValueKind == JsonValueKind.Array
                            ? p.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? "").ToList()
                            : new List<string>();
                        sections.Add(new GeneratedSection(heading, paras));
                    }
                }
                if (root.TryGetProperty("ConclusionParagraphs", out var cp) && cp.ValueKind == JsonValueKind.Array)
                    conclusionParagraphs = cp.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? "").ToList();
                if (root.TryGetProperty("CallToAction", out var cta) && cta.ValueKind == JsonValueKind.String)
                    callToAction = cta.GetString() ?? "";
            }
            catch
            {
                // Artifact parse failure — fall through to action run parsing
            }
        }

        // Also check action runs for any additional enrichment (humanize, de-opt, etc.)
        foreach (var run in completedRuns)
        {
            if (string.IsNullOrWhiteSpace(run.OutputSnapshotJson))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(run.OutputSnapshotJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("title", out var titleValue) && titleValue.ValueKind == JsonValueKind.String)
                    title = titleValue.GetString() ?? title;
                if (root.TryGetProperty("summary", out var summaryValue) && summaryValue.ValueKind == JsonValueKind.String)
                    summary = summaryValue.GetString() ?? summary;
                if (root.TryGetProperty("metaDescription", out var metaValue) && metaValue.ValueKind == JsonValueKind.String)
                    metaDescription = metaValue.GetString() ?? metaDescription;
                if (root.TryGetProperty("bodyText", out var bodyValue) && bodyValue.ValueKind == JsonValueKind.String)
                    bodyText = bodyValue.GetString() ?? bodyText;
            }
            catch
            {
                // ignore malformed action output
            }
        }

        // Build full bodyText from structured content if we have it
        if (string.IsNullOrWhiteSpace(bodyText) && (introParagraphs.Count > 0 || sections.Count > 0))
        {
            var parts = new List<string>();
            foreach (var p in introParagraphs) parts.Add(p);
            foreach (var s in sections)
            {
                parts.Add($"## {s.Heading}");
                foreach (var p in s.Paragraphs) parts.Add(p);
            }
            foreach (var p in conclusionParagraphs) parts.Add(p);
            if (!string.IsNullOrWhiteSpace(callToAction)) parts.Add(callToAction);
            bodyText = string.Join("\n\n", parts);
        }

        if (string.IsNullOrWhiteSpace(slug))
            slug = BuildSlug(title);

        var wordCount = bodyText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return new GeneratedLongformArticle(
            title,
            slug,
            summary,
            metaDescription,
            targetWordCountMin,
            targetWordCountMax,
            wordCount,
            Math.Max(1, wordCount / 250),
            introParagraphs,
            sections,
            conclusionParagraphs,
            callToAction,
            bodyText,
            bodyText,
            false,
            true);
    }

    private static ContentWorkflowTask BuildCompatTask(WorkflowDefinitionRun workflowRun, WorkflowActionRun actionRun, WorkflowActionDefinition actionDefinition)
    {
        return new ContentWorkflowTask
        {
            Id = actionRun.Id,
            ContentWorkflowJobId = workflowRun.Id,
            Name = actionDefinition.Name,
            StageName = actionDefinition.Name,
            AssignedAgent = actionDefinition.AssignedAgent,
            DisplayOrder = actionDefinition.Order,
            Instructions = actionDefinition.Instructions,
            InputDataJson = actionRun.InputSnapshotJson ?? workflowRun.InputSnapshotJson,
            OutputDataJson = actionRun.OutputSnapshotJson ?? "{}",
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>Restores writer research fields from the approved idea snapshot.</summary>
    /// <param name="idea">The immutable approved topic direction and research snapshot.</param>
    /// <returns>A compatibility idea preserving camelCase frozen snapshots and PascalCase candidate snapshots.</returns>
    /// <remarks>Do not silently replace a research keyword with the full display title. Legacy snapshots are hydrated separately.</remarks>
    private static ContentIdea BuildCompatIdea(IdeaRecord idea)
    {
        using var snapshot = JsonDocument.Parse(string.IsNullOrWhiteSpace(idea.IdeaSnapshotJson) ? "{}" : idea.IdeaSnapshotJson);
        var root = snapshot.RootElement;
        bool TryRead(string name, out JsonElement value) => root.TryGetProperty(name, out value)
            || root.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
        string ReadString(string name, string fallback = "") => TryRead(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;
        var secondaryJson = ReadString("secondaryKeywordsJson");
        if (string.IsNullOrWhiteSpace(secondaryJson))
            secondaryJson = TryRead("secondaryKeywords", out var secondary) && secondary.ValueKind == JsonValueKind.Array ? secondary.GetRawText() : "[]";
        var sourcesJson = ReadString("sourceSummaryJson");
        if (string.IsNullOrWhiteSpace(sourcesJson))
        {
            var findings = TryRead("supportingFindings", out var sources) && sources.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<ResearchFinding>>(sources.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [] : [];
            sourcesJson = JsonSerializer.Serialize(findings.Take(8).Select(ResearchEvidenceHandoff.ToWriterSummary));
        }
        return new ContentIdea
        {
            Id = ExtractLegacyIdeaId(idea),
            SiteId = idea.SiteId ?? Guid.Empty,
            Title = idea.IdeaTitle,
            PrimaryKeyword = ReadString("primaryKeyword"),
            SecondaryKeywordsJson = secondaryJson,
            SourceSummaryJson = sourcesJson,
            Summary = ReadString("summary", idea.ReaderProblem),
            ContentType = ReadString("contentType", "LongFormBlogArticle"),
            SlugSuggestion = ReadString("slugSuggestion"),
            SearchIntent = idea.SearchIntent,
            AudiencePainPoint = idea.ReaderProblem,
            AudienceGoal = idea.AudienceType,
            RecommendedAngle = idea.UniquenessAngle,
            WhyNow = idea.EmotionalTrigger,
            Status = idea.Status.ToString(),
            CreatedUtc = idea.CreatedUtc,
            UpdatedUtc = idea.UpdatedUtc
        };
    }

    private static Guid ExtractLegacyIdeaId(IdeaRecord idea)
    {
        try
        {
            using var doc = JsonDocument.Parse(idea.IdeaSnapshotJson);
            if (doc.RootElement.TryGetProperty("legacyContentIdeaId", out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // ignore bridge parse failures
        }

        return Guid.Empty;
    }
    private static string BuildSlug(string title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Guid.NewGuid().ToString("n")
            : string.Join('-', title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Writes and persists a model draft only when generation succeeds.</summary>
    /// <remarks>Synthetic templates are diagnostic output and must not enter the persisted DraftArticle stream.</remarks>
    /// <param name="coordinator">The existing article generation pipeline.</param>
    /// <param name="workflowRun">The run receiving the draft artifact.</param>
    /// <param name="actionRun">The current writer action instance.</param>
    /// <param name="actionDefinition">The writer capability definition.</param>
    /// <param name="idea">The approved idea record.</param>
    /// <param name="article">The article state before writing.</param>
    /// <param name="legacyTask">The task representation consumed by the coordinator.</param>
    /// <param name="legacyIdea">The approved direction and preserved research fields.</param>
    /// <param name="cancellationToken">Cancels writing and persistence.</param>
    /// <returns>A structured, non-synthetic draft.</returns>
    /// <exception cref="InvalidOperationException">Writer generation did not produce a real draft.</exception>
    private async Task<object> BuildAndWriteDraftAsync(
        WorkflowCoordinatorAgent coordinator,
        WorkflowDefinitionRun workflowRun,
        WorkflowActionRun actionRun,
        WorkflowActionDefinition actionDefinition,
        IdeaRecord idea,
        GeneratedLongformArticle article,
        ContentWorkflowTask legacyTask,
        ContentIdea legacyIdea,
        CancellationToken cancellationToken)
    {
        // Actually invoke the LLM writer via the coordinator's full draft pipeline
        var writtenArticle = await coordinator.ResolveArticleAsync(legacyTask, legacyIdea, cancellationToken);

        if (writtenArticle.IsSynthetic)
            throw new InvalidOperationException("Draft generation returned synthetic content. Retry with a working writer before continuing.");

        // Persist the enriched article back so subsequent actions in this dispatch cycle get real content
        // Update the dispatch result to carry the enriched article
        var draftPayload = new WorkflowArticleDraft
        {
            Title = writtenArticle.Title,
            Slug = writtenArticle.Slug,
            Summary = writtenArticle.Summary,
            MetaDescription = writtenArticle.MetaDescription,
            ContentType = legacyIdea?.ContentType ?? "LongFormBlogArticle",
            TargetWordCountMin = writtenArticle.TargetWordCountMin,
            TargetWordCountMax = writtenArticle.TargetWordCountMax,
            EstimatedWordCount = writtenArticle.EstimatedWordCount,
            EstimatedReadTimeMinutes = writtenArticle.EstimatedReadTimeMinutes,
            IntroParagraphs = writtenArticle.IntroParagraphs.ToList(),
            Sections = writtenArticle.Sections.Select(s => new WorkflowArticleSectionDraft
            {
                Heading = s.Heading,
                Paragraphs = s.Paragraphs.ToList()
            }).ToList(),
            ConclusionParagraphs = writtenArticle.ConclusionParagraphs.ToList(),
            CallToAction = writtenArticle.CallToAction
        };

        // Also save the full article as an artifact so the article review page can find it
        var artifact = new ContentArtifact
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = workflowRun.Id,
            ArtifactType = "DraftArticle",
            Title = writtenArticle.Title,
            ContentJson = JsonSerializer.Serialize(draftPayload),
            CreatedUtc = DateTime.UtcNow
        };
        _dbContext.ContentArtifacts.Add(artifact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return draftPayload;
    }
}

public sealed record NewWorkflowDispatchResult(object Payload, GeneratedLongformArticle Article);
