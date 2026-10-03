using System.Diagnostics;
using ContentOS.Infrastructure.Workflow;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ContentOS.Infrastructure.Research;

namespace ContentOS.Infrastructure.Research;

/// <summary>Collects research for personal publishing and saves reviewable ideas with their writer context.</summary>
public class ResearchAgent : IResearchAgent
{
    private readonly ContentOsDbContext _dbContext;
    private readonly IEnumerable<IResearchSourceProvider> _providers;
    private readonly IIdeationAgent _ideationAgent;
    private readonly IIdeaDeduplicator _deduplicator;
    private readonly ILogger<ResearchAgent> _logger;

    public ResearchAgent(
        ContentOsDbContext dbContext,
        IEnumerable<IResearchSourceProvider> providers,
        IIdeationAgent ideationAgent,
        IIdeaDeduplicator deduplicator,
        ILogger<ResearchAgent> logger)
    {
        _dbContext = dbContext;
        _providers = providers;
        _ideationAgent = ideationAgent;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    /// <summary>Collects research and saves reviewable ideas with their writer context.</summary>
    /// <param name="siteId">The active personal publishing site.</param>
    /// <param name="requestedIdeaCount">Requested number of ideas, bounded to the supported range.</param>
    /// <param name="cancellationToken">Cancels research and persistence.</param>
    /// <returns>The saved idea counts and titles.</returns>
    /// <remarks>Freeze research fields in the idea snapshot so later article dispatch does not reconstruct keywords from display titles.</remarks>
    public async Task<RunResearchResult> RunAsync(Guid siteId, int? requestedIdeaCount = null, CancellationToken cancellationToken = default)
    {
        var site = await _dbContext.Sites.FirstOrDefaultAsync(x => x.Id == siteId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Site not found or inactive.");

        var researchOperationId = Guid.NewGuid();
        var started = Stopwatch.GetTimestamp();
        using var diagnosticScope = _logger.BeginScope(new Dictionary<string, object> { ["ResearchOperationId"] = researchOperationId, ["SiteId"] = site.Id });
        _logger.LogInformation("Research started. Collected evidence will support reviewable ideas; topic relevance is not verified. ResearchOperationId={ResearchOperationId} SiteId={SiteId} Outcome={Outcome}", researchOperationId, site.Id, "started");
        var targetIdeaCount = Math.Clamp(requestedIdeaCount ?? 15, 10, 30);

        var context = new ResearchContext
        {
            SiteId = site.Id,
            SiteName = site.Name,
            SiteUrl = site.Domain,
            Niche = site.Niche,
            AudienceDescription = site.DefaultTone,
            MonetizationGoals = site.Niche,
            SeedTopics = BuildSeedTopics(site),
            MaxFindingsPerProvider = 8,
            MaxIdeasToSave = targetIdeaCount
        };

        var findings = new List<ResearchFinding>();
        foreach (var provider in _providers)
        {
            var providerStarted = Stopwatch.GetTimestamp();
            var providerId = WorkflowDiagnostics.Identifier(provider.GetType().Name);
            _logger.LogInformation("Collecting research sources. Review provider outcome and excerpt counts before relying on evidence. ResearchOperationId={ResearchOperationId} Provider={Provider} Outcome={Outcome}", researchOperationId, providerId, "started");
            IReadOnlyCollection<ResearchFinding> providerFindings;
            try
            {
                providerFindings = await provider.ResearchAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                var failure = WorkflowDiagnostics.Failure(ex);
                _logger.LogWarning("Research collection ended without a result. {NextAction} ResearchOperationId={ResearchOperationId} Provider={Provider} Outcome={Outcome} ElapsedMilliseconds={ElapsedMilliseconds} FailureCode={FailureCode} ExceptionType={ExceptionType}", cancellationToken.IsCancellationRequested ? "The caller cancelled; resume when ready." : failure.Summary, researchOperationId, providerId, cancellationToken.IsCancellationRequested ? "cancelled" : "failed", (long)Stopwatch.GetElapsedTime(providerStarted).TotalMilliseconds, cancellationToken.IsCancellationRequested ? "CallerCancellation" : failure.Code, failure.ExceptionType);
                throw;
            }
            _logger.LogInformation("Research provider collection finished. Inspect counts before using evidence. ResearchOperationId={ResearchOperationId} Provider={Provider} Outcome={Outcome} ElapsedMilliseconds={ElapsedMilliseconds}", researchOperationId, providerId, "completed", (long)Stopwatch.GetElapsedTime(providerStarted).TotalMilliseconds);
            findings.AddRange(providerFindings);
            _logger.LogInformation("Research collected source records. Usable excerpts can support the brief; source relevance and factual claims are not verified. ResearchOperationId={ResearchOperationId} Provider={Provider} SourceCount={SourceCount} UsableExcerptCount={UsableExcerptCount}", researchOperationId, WorkflowDiagnostics.Identifier(provider.GetType().Name), providerFindings.Count, providerFindings.Count(f => !string.IsNullOrWhiteSpace(ResearchEvidenceHandoff.CleanExcerpt(f.SourceExcerpt))));
        }

        var filtered = (await _ideationAgent.GenerateIdeasAsync(context, findings, cancellationToken)).ToList();

        var existingIdeas = await _dbContext.ContentIdeas
            .Where(x => x.SiteId == site.Id && x.Status != "Archived")
            .ToListAsync(cancellationToken);

        var duplicatesSkipped = 0;
        var savedTitles = new List<string>();

        var ideaDefinition = await _dbContext.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowType == WorkflowDefinitionType.Idea && x.IsActive, cancellationToken);

        var ideaRun = ideaDefinition is null
            ? null
            : new WorkflowDefinitionRun
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionFamilyId = ideaDefinition.WorkflowDefinitionFamilyId,
                WorkflowDefinitionId = ideaDefinition.Id,
                WorkflowType = ideaDefinition.WorkflowType,
                Version = ideaDefinition.Version,
                Status = WorkflowDefinitionRunStatus.Completed,
                StartedUtc = DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
                TriggeredBy = "ResearchAgent",
                InputSnapshotJson = JsonSerializer.Serialize(new { siteId, findings = findings.Count, candidates = filtered.Count })
            };

        if (ideaRun is not null)
        {
            _dbContext.WorkflowDefinitionRuns.Add(ideaRun);
        }

        foreach (var candidate in filtered)
        {
            // --- USE CENTRALIZED DEDUPLICATION SERVICE ---
            if (_deduplicator.IsDuplicate(candidate, existingIdeas))
            {
                duplicatesSkipped++;
                continue;
            }

            var now = DateTime.UtcNow;


            var idea = new ContentIdea
            {
                Id = Guid.NewGuid(),
                SiteId = site.Id,
                Title = candidate.Title,
                SlugSuggestion = BuildSlug(candidate.Title),
                PrimaryKeyword = candidate.PrimaryKeyword,
                SecondaryKeywordsJson = JsonSerializer.Serialize(candidate.SecondaryKeywords),
                SearchIntent = candidate.SearchIntent,
                AudiencePainPoint = candidate.AudiencePainPoint,
                AudienceGoal = candidate.AudienceGoal,
                RecommendedAngle = candidate.RecommendedAngle,
                Summary = candidate.Summary,
                WhyNow = candidate.WhyNow,
                ContentType = candidate.ContentType,
                FunnelStage = candidate.ContentBucket,
                MonetizationFitScore = candidate.MonetizationFitScore,
                SeoOpportunityScore = candidate.SeoOpportunityScore,
                TrendScore = candidate.AudienceFitScore,
                CompetitionScore = candidate.CompetitionDifficultyScore,
                OverallScore = candidate.OverallScore,
                Evergreen = candidate.Evergreen,
                Seasonal = candidate.Seasonal,
                TopicType = candidate.TopicTypeLabel,
                SpecificityTag = candidate.SpecificityTag,
                IntentMatchScore = candidate.IntentMatchScore,
                UniquenessScore = candidate.UniquenessScore,
                ClickPotentialScore = candidate.ClickPotentialScore,
                MonetizationPotentialScore = candidate.MonetizationPotentialScore,
                IsHighCompetition = candidate.IsHighCompetition,
                CompetitionModifier = candidate.CompetitionModifier,
                LowCompetitionBoost = candidate.LowCompetitionBoost,
                MonetizationViable = candidate.MonetizationViable,
                MonetizationWarning = candidate.MonetizationWarning,
                // --- PERSIST DEDUPLICATION METADATA ---
                CanonicalTopic = _deduplicator.ExtractCanonicalTopic(candidate.Title, candidate.PrimaryKeyword),
                Angle = _deduplicator.ExtractAngle(candidate.Title, candidate.PrimaryKeyword, candidate.RecommendedAngle),
                Intent = _deduplicator.ExtractIntent(candidate.SearchIntent),
                PainPoint = _deduplicator.ExtractPainPoint(candidate.AudiencePainPoint),
                SourceSummaryJson = JsonSerializer.Serialize(candidate.SupportingFindings
                    .Select(ResearchEvidenceHandoff.ToWriterSummary)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .Take(8)
                    .ToList()),
                Status = "NeedsReview",
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.ContentIdeas.Add(idea);

            foreach (var finding in candidate.SupportingFindings.Take(8))
            {
                var sourceId = Guid.NewGuid();
                _dbContext.ContentResearchSources.Add(new ContentResearchSource
                {
                    Id = sourceId,
                    ContentIdeaId = idea.Id,
                    SourceType = finding.SourceType,
                    SourceTitle = finding.SourceTitle,
                    SourceUrl = finding.SourceUrl,
                    Notes = BuildSourceNotes(finding),
                    CreatedUtc = now
                });
                _logger.LogInformation("A collected source was linked to an idea for review. ResearchOperationId={ResearchOperationId} IdeaId={IdeaId} SourceId={SourceId}", researchOperationId, idea.Id, sourceId);
            }

            if (ideaRun is not null && ideaDefinition is not null)
            {
                var ideaRecord = new IdeaRecord
                {
                    Id = Guid.NewGuid(),
                    SourceWorkflowRunId = ideaRun.Id,
                    SourceWorkflowDefinitionId = ideaDefinition.Id,
                    WorkflowVersion = ideaDefinition.Version,
                    SiteId = site.Id,
                    IdeaTitle = candidate.Title,
                    ReaderProblem = candidate.AudiencePainPoint,
                    AudienceType = candidate.AudienceGoal,
                    SearchIntent = candidate.SearchIntent,
                    EmotionalTrigger = candidate.WhyNow,
                    UniquenessAngle = candidate.RecommendedAngle,
                    MonetizationFit = candidate.MonetizationFitScore,
                    SeoPotential = candidate.SeoOpportunityScore,
                    Difficulty = candidate.CompetitionDifficultyScore,
                    PriorityScore = candidate.OverallScore,
                    Status = IdeaRecordStatus.Pending,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    IdeaSnapshotJson = JsonSerializer.Serialize(new
                    {
                        ideaTitle = candidate.Title,
                        readerProblem = candidate.AudiencePainPoint,
                        audienceType = candidate.AudienceGoal,
                        searchIntent = candidate.SearchIntent,
                        emotionalTrigger = candidate.WhyNow,
                        uniquenessAngle = candidate.RecommendedAngle,
                        monetizationFit = candidate.MonetizationFitScore,
                        seoPotential = candidate.SeoOpportunityScore,
                        seoMeasurementStatus = candidate.SeoMeasurementStatus,
                        competitionEvidenceStatus = candidate.CompetitionEvidenceStatus,
                        difficulty = candidate.CompetitionDifficultyScore,
                        priorityScore = candidate.OverallScore,
                        editorialQualityScore = candidate.EditorialQualityScore,
                        editorialScoringStatus = candidate.EditorialScoringStatus,
                        editorialAssessment = candidate.EditorialAssessment,
                        editorialRubric = context.IdeaEditorialRubric,
                        automaticChecks = new { collectedSourceReferencesPresent = candidate.SupportingFindings.Count > 0, editorialAssessmentStructurallyValid = candidate.EditorialScoringStatus == "AssessedModelOpinion", sourceRelevanceVerified = false },
                        measuredSeo = new { status = "Unknown", searchVolume = (decimal?)null, rankingDifficulty = (decimal?)null, demandTrend = (decimal?)null },
                        primaryKeyword = idea.PrimaryKeyword,
                        secondaryKeywordsJson = idea.SecondaryKeywordsJson,
                        sourceSummaryJson = idea.SourceSummaryJson,
                        summary = idea.Summary,
                        contentType = idea.ContentType,
                        slugSuggestion = idea.SlugSuggestion,
                        legacyContentIdeaId = idea.Id
                    }),
                    // --- PERSIST DEDUPLICATION METADATA ---
                    CanonicalTopic = _deduplicator.ExtractCanonicalTopic(candidate.Title, candidate.PrimaryKeyword),
                    Angle = _deduplicator.ExtractAngle(candidate.Title, candidate.PrimaryKeyword, candidate.RecommendedAngle),
                    Intent = _deduplicator.ExtractIntent(candidate.SearchIntent),
                    PainPoint = _deduplicator.ExtractPainPoint(candidate.AudiencePainPoint)
                };

                _dbContext.IdeaRecords.Add(ideaRecord);
            }

            existingIdeas.Add(idea);
            savedTitles.Add(idea.Title);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Research completed. Review the saved ideas and collected sources before approving a brief; relevance is not verified. SiteId={SiteId} Findings={Findings} Candidates={Candidates} Saved={Saved} DuplicatesSkipped={DuplicatesSkipped} ResearchOperationId={ResearchOperationId} ElapsedMilliseconds={ElapsedMilliseconds} Outcome={Outcome}",
            site.Id,
            findings.Count,
            filtered.Count,
            savedTitles.Count,
            duplicatesSkipped, researchOperationId, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, "completed");

        return new RunResearchResult
        {
            SiteId = site.Id,
            RequestedIdeaCount = targetIdeaCount,
            FindingsGathered = findings.Count,
            CandidatesCreated = filtered.Count,
            IdeasSaved = savedTitles.Count,
            DuplicatesSkipped = duplicatesSkipped,
            SavedTitles = savedTitles,
            ShortfallReason = savedTitles.Count < targetIdeaCount
                ? $"Saved {savedTitles.Count} of {targetIdeaCount} requested ideas. Collected {findings.Count} source findings; {filtered.Count} candidates remained after evidence and quality filtering; {duplicatesSkipped} ledger duplicates were skipped. No filler ideas were added. Collected source relevance still requires review."
                : null
        };
    }

    // --- LOCAL HELPERS (NOT USED FOR DEDUPLICATION LOGIC) ---
    private static string BuildSourceNotes(ResearchFinding finding)
    {
        var parts = new List<string>();

        var excerpt = ResearchEvidenceHandoff.CleanExcerpt(finding.SourceExcerpt);
        if (!string.IsNullOrWhiteSpace(excerpt)) parts.Add($"Untrusted collected excerpt: {excerpt}");

        if (!string.IsNullOrWhiteSpace(finding.ObservedPhrase)) parts.Add($"Observed: {CleanDashboardText(finding.ObservedPhrase)}");
        if (!string.IsNullOrWhiteSpace(finding.PainPoint)) parts.Add($"Pain point: {CleanDashboardText(finding.PainPoint)}");
        if (!string.IsNullOrWhiteSpace(finding.Notes)) parts.Add(CleanDashboardText(finding.Notes));

        return string.Join(" | ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string CleanDashboardText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Trim();

        foreach (var separator in new[] { " - ", " | ", " — " })
        {
            var index = cleaned.IndexOf(separator, StringComparison.Ordinal);
            if (index > 24)
            {
                cleaned = cleaned[..index].Trim();
                break;
            }
        }

        cleaned = cleaned.Replace("\n", " ").Replace("\r", " ").Replace('"', '\'');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ToLowerPhrase(string value)
    {
        var cleaned = value.Trim().TrimEnd('.');
        return cleaned.Length == 0 ? cleaned : char.ToLowerInvariant(cleaned[0]) + cleaned[1..];
    }

    private static string BuildSlug(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static List<string> BuildSeedTopics(Site site)
    {
        if (site.Domain.Contains("zerotoherobudgeting", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "zero based budgeting for beginners",
                "zero based budgeting categories",
                "budgeting with irregular income",
                "budgeting for low income households",
                "budgeting for families",
                "budgeting for couples",
                "budgeting for single moms",
                "budgeting when bills hit on different dates",
                "budgeting with variable bills",
                "how to start budgeting when overwhelmed",
                "why budgets fail",
                "stop overspending groceries",
                "meal planning on a budget",
                "sinking funds for beginners",
                "emergency fund vs sinking fund",
                "cash stuffing alternatives",
                "budget planner printable",
                "budget binder ideas",
                "simple budgeting app for beginners",
                "best budget apps for beginners",
                "weekly vs monthly budgeting",
                "paycheck budgeting",
                "budgeting after a pay cut",
                "budgeting during inflation",
                "subscription creep budgeting",
                "back to school budgeting",
                "holiday budget planning",
                "christmas sinking fund",
                "no spend challenge budgeting",
                "buy now pay later budgeting",
                "debt payoff budget",
                "beginner budget mistakes",
                "first monthly budget",
                "budgeting with ADHD",
                "budgeting when you hate budgeting"
            ];
        }

        return [site.Niche];
    }
}
