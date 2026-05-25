using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ContentOS.Infrastructure.Research;

namespace ContentOS.Infrastructure.Research;

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

    public async Task<RunResearchResult> RunAsync(Guid siteId, int? requestedIdeaCount = null, CancellationToken cancellationToken = default)
    {
        var site = await _dbContext.Sites.FirstOrDefaultAsync(x => x.Id == siteId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Site not found or inactive.");

        var targetIdeaCount = Math.Clamp(requestedIdeaCount ?? 15, 10, 20);

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
            var providerFindings = await provider.ResearchAsync(context, cancellationToken);
            findings.AddRange(providerFindings);
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
            _logger.LogWarning(
                "Saving research idea {Title} | keyword={Keyword} | painPoint={PainPoint} | goal={Goal} | angle={Angle} | whyNow={WhyNow}",
                candidate.Title,
                candidate.PrimaryKeyword,
                candidate.AudiencePainPoint,
                candidate.AudienceGoal,
                candidate.RecommendedAngle,
                candidate.WhyNow);

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
                    .Select(x => $"{x.SourceType}: {x.SourceTitle}")
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
                _dbContext.ContentResearchSources.Add(new ContentResearchSource
                {
                    Id = Guid.NewGuid(),
                    ContentIdeaId = idea.Id,
                    SourceType = finding.SourceType,
                    SourceTitle = finding.SourceTitle,
                    SourceUrl = finding.SourceUrl,
                    Notes = BuildSourceNotes(finding),
                    CreatedUtc = now
                });
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
                        difficulty = candidate.CompetitionDifficultyScore,
                        priorityScore = candidate.OverallScore,
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
            "Research run completed for site {SiteId}. Findings={Findings} Candidates={Candidates} Saved={Saved} DuplicatesSkipped={DuplicatesSkipped}",
            site.Id,
            findings.Count,
            filtered.Count,
            savedTitles.Count,
            duplicatesSkipped);

        return new RunResearchResult
        {
            SiteId = site.Id,
            RequestedIdeaCount = targetIdeaCount,
            FindingsGathered = findings.Count,
            CandidatesCreated = filtered.Count,
            IdeasSaved = savedTitles.Count,
            DuplicatesSkipped = duplicatesSkipped,
            SavedTitles = savedTitles
        };
    }

    // --- LOCAL HELPERS (NOT USED FOR DEDUPLICATION LOGIC) ---
    private static string BuildSourceNotes(ResearchFinding finding)
    {
        var parts = new List<string>();

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