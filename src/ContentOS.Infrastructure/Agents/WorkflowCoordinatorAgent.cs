using ContentOS.Infrastructure.Workflow;
using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Image;
using ContentOS.Infrastructure.Research.Serp;
using ContentOS.Infrastructure.Scoring;
using ContentOS.Infrastructure.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ContentOS.Infrastructure.Agents;

public interface IWorkflowCoordinatorAgent
{
    Task ProcessPendingTasksAsync(CancellationToken cancellationToken = default);
}

/// <summary>Coordinates article stages and retains the distinction between model drafts and diagnostic templates.</summary>
public class WorkflowCoordinatorAgent : IWorkflowCoordinatorAgent
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<WorkflowCoordinatorAgent> _logger;
    private readonly IWorkflowArticleWriter _workflowArticleWriter;
    private readonly ISerpBenchmarkService _serpBenchmarkService;
    private readonly IOptimizationScoringService _optimizationScoringService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IContentStrategyAgent _contentStrategyAgent;
    private readonly IEditorialAgent _editorialAgent;
    private readonly ISeoAndMonetizationAgent _seoAndMonetizationAgent;
    private readonly IQaAndComplianceAgent _qaAndComplianceAgent;
    private readonly IQaFixVerificationAgent _qaFixVerificationAgent;
    private readonly ISeoOptimizationAgent _seoOptimizationAgent;
    private readonly ILightQaAgent _lightQaAgent;
    private readonly IWorkflowTaskDispatcher _taskDispatcher;
    private static readonly SemaphoreSlim _processingLock = new(1, 1);

    public WorkflowCoordinatorAgent(
        ContentOsDbContext dbContext, 
        ILogger<WorkflowCoordinatorAgent> logger, 
        IWorkflowArticleWriter workflowArticleWriter, 
        ISerpBenchmarkService serpBenchmarkService, 
        IOptimizationScoringService optimizationScoringService,
        IImageGenerationService imageGenerationService,
        IContentStrategyAgent contentStrategyAgent,
        IEditorialAgent editorialAgent,
        ISeoAndMonetizationAgent seoAndMonetizationAgent,
        IQaAndComplianceAgent qaAndComplianceAgent,
        IQaFixVerificationAgent qaFixVerificationAgent,
        ISeoOptimizationAgent seoOptimizationAgent,
        ILightQaAgent lightQaAgent,
        IWorkflowTaskDispatcher taskDispatcher)
    {
        _dbContext = dbContext;
        _logger = logger;
        _workflowArticleWriter = workflowArticleWriter;
        _serpBenchmarkService = serpBenchmarkService;
        _optimizationScoringService = optimizationScoringService;
        _imageGenerationService = imageGenerationService;
        _contentStrategyAgent = contentStrategyAgent;
        _editorialAgent = editorialAgent;
        _seoAndMonetizationAgent = seoAndMonetizationAgent;
        _qaAndComplianceAgent = qaAndComplianceAgent;
        _qaFixVerificationAgent = qaFixVerificationAgent;
        _seoOptimizationAgent = seoOptimizationAgent;
        _lightQaAgent = lightQaAgent;
        _taskDispatcher = taskDispatcher;
    }

    public async Task ProcessPendingTasksAsync(CancellationToken cancellationToken = default)
    {
        if (!await _processingLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogInformation("Workflow Coordinator: another cycle already running, skipping.");
            return;
        }

        try
        {
            _logger.LogInformation("Workflow Coordinator: Scanning for ready tasks...");

        var task = await _dbContext.ContentWorkflowTasks
            .Where(t => t.Status == ContentOS.Domain.Enums.TaskStatus.Ready)
            .OrderBy(t => t.ContentWorkflowJobId)
            .ThenBy(t => t.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (task is null)
        {
            _logger.LogInformation("No ready tasks found.");
            return;
        }

        _logger.LogInformation("Processing task {TaskName} for Job {JobId} assigned to {Agent}", 
            task.Name, task.ContentWorkflowJobId, task.AssignedAgent);

        if (!AgentStack.CanonicalAgents.Contains(task.AssignedAgent))
        {
            _logger.LogWarning("Task {TaskId} uses non-canonical agent name {Agent}", task.Id, task.AssignedAgent);
        }

        try
        {
            // Detach any stale tracked entities to avoid concurrency issues
            _dbContext.ChangeTracker.Clear();
            
            // Re-fetch the task to get a fresh, untracked version
            var freshTask = await _dbContext.ContentWorkflowTasks.FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);
            if (freshTask is null || freshTask.Status != ContentOS.Domain.Enums.TaskStatus.Ready)
            {
                return;
            }
            task = freshTask;
            
            task.Status = ContentOS.Domain.Enums.TaskStatus.InProgress;
            task.StartedUtc = task.StartedUtc ?? DateTime.UtcNow;
            task.LastUpdatedUtc = DateTime.UtcNow;

            var job = await _dbContext.ContentWorkflowJobs
                .FirstOrDefaultAsync(j => j.Id == task.ContentWorkflowJobId, cancellationToken);

            if (job is not null)
            {
                job.Status = "InProgress";
                job.StartedUtc = job.StartedUtc ?? DateTime.UtcNow;
                job.CurrentStage = task.StageName;
                job.LastUpdatedUtc = DateTime.UtcNow;
                job.ErrorMessage = string.Empty;
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict on task {TaskId}, skipping", task.Id);
                // Another instance already processed this task - just return and let the next cycle pick it up
                return;
            }

            await ExecuteTaskAsync(task, cancellationToken);
        }
        catch (Exception ex)
            {
                LogSafeFailure(ex, "The workflow task failed. Review its failure code before retrying.", task.ContentWorkflowJobId);

                if (await TryQueueAutomaticRewriteAsync(task, ex.Message, cancellationToken))
                {
                    return;
                }

                task.Status = ContentOS.Domain.Enums.TaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.LastUpdatedUtc = DateTime.UtcNow;

                var job = await _dbContext.ContentWorkflowJobs
                    .FirstOrDefaultAsync(j => j.Id == task.ContentWorkflowJobId, cancellationToken);

                if (job is not null)
                {
                    job.Status = "Failed";
                    job.ErrorMessage = ex.Message;
                    job.LastUpdatedUtc = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task<bool> TryQueueAutomaticRewriteAsync(ContentWorkflowTask failedTask, string failureReason, CancellationToken cancellationToken)
    {
        if (!string.Equals(failedTask.Name, "Run Strict QA Scoring", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const int maxAutomaticRewriteAttempts = 5;
        if (failedTask.RetryCount >= maxAutomaticRewriteAttempts)
        {
            _logger.LogWarning("QA task {TaskId} exhausted automatic rewrite attempts.", failedTask.Id);
            return false;
        }

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == failedTask.ContentWorkflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        var writerTask = tasks.FirstOrDefault(x => string.Equals(x.Name, "Write Rule-Compliant Draft", StringComparison.OrdinalIgnoreCase));
        if (writerTask is null)
        {
            return false;
        }

        // Extract the QA rework directive from the failed task's output
        ContentOS.Domain.Qa.QaReworkDirective? reworkDirective = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(failedTask.OutputDataJson))
            {
                var qaReport = JsonSerializer.Deserialize<QaReportResult>(failedTask.OutputDataJson, JsonOptionsWithCaseInsensitive);
                reworkDirective = qaReport?.ReworkDirective;
            }
        }
        catch (Exception ex)
        {
            LogSafeFailure(ex, "The QA rewrite instructions could not be read. Review the saved QA result.", failedTask.ContentWorkflowJobId);
        }

        var taskIdsToReset = tasks
            .Where(x => x.DisplayOrder >= writerTask.DisplayOrder)
            .Select(x => x.Id)
            .ToHashSet();

        var artifactsToDelete = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == failedTask.ContentWorkflowJobId &&
                        ((x.ContentWorkflowTaskId.HasValue && taskIdsToReset.Contains(x.ContentWorkflowTaskId.Value)) ||
                         x.ArtifactType == "FinalArticlePreview"))
            .ToListAsync(cancellationToken);

        // Snapshot the pre-rework draft before deleting downstream artifacts
        string? preReworkSnapshotId = null;
        var writerOutput = tasks.FirstOrDefault(t => t.Id == writerTask.Id)?.OutputDataJson;
        if (!string.IsNullOrWhiteSpace(writerOutput) && writerOutput != "{}")
        {
            var snapshotId = Guid.NewGuid();
            _dbContext.ContentArtifacts.Add(new ContentArtifact
            {
                Id = snapshotId,
                ContentWorkflowJobId = failedTask.ContentWorkflowJobId,
                ContentWorkflowTaskId = writerTask.Id,
                ArtifactType = "PreReworkDraftSnapshot",
                Title = $"Pre-rework draft snapshot (before attempt {failedTask.RetryCount + 1})",
                StorageType = "Database",
                ContentJson = writerOutput,
                ContentText = "Snapshot of article state before QA-triggered rework",
                VersionNumber = failedTask.RetryCount + 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedByAgent = AgentStack.QaScoringAgent
            });
            preReworkSnapshotId = snapshotId.ToString();
        }

        if (artifactsToDelete.Count > 0)
        {
            _dbContext.ContentArtifacts.RemoveRange(artifactsToDelete);
        }

        // Preserve the QA rework directive as an artifact
        if (reworkDirective is not null)
        {
            var directiveWithSnapshot = reworkDirective with
            {
                AttemptNumber = failedTask.RetryCount + 1,
                PreReworkSnapshotId = preReworkSnapshotId
            };
            _dbContext.ContentArtifacts.Add(new ContentArtifact
            {
                Id = Guid.NewGuid(),
                ContentWorkflowJobId = failedTask.ContentWorkflowJobId,
                ContentWorkflowTaskId = failedTask.Id,
                ArtifactType = "QaReworkDirective",
                Title = $"QA rework directive (attempt {failedTask.RetryCount + 1})",
                StorageType = "Database",
                ContentJson = JsonSerializer.Serialize(directiveWithSnapshot, JsonOptions),
                ContentText = $"{directiveWithSnapshot.Summary}. Fixes: {string.Join("; ", directiveWithSnapshot.RequiredFixes)}",
                VersionNumber = failedTask.RetryCount + 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedByAgent = AgentStack.QaScoringAgent
            });
        }

        failedTask.RetryCount += 1;
        var now = DateTime.UtcNow;

        foreach (var workflowTask in tasks)
        {
            if (workflowTask.DisplayOrder < writerTask.DisplayOrder)
            {
                continue;
            }

            workflowTask.Status = workflowTask.Id == writerTask.Id ? ContentOS.Domain.Enums.TaskStatus.Ready : ContentOS.Domain.Enums.TaskStatus.Pending;
            workflowTask.StartedUtc = null;
            workflowTask.CompletedUtc = null;
            workflowTask.ErrorMessage = string.Empty;
            workflowTask.OutputDataJson = "{}";
            workflowTask.LastUpdatedUtc = now;

            workflowTask.InputDataJson = workflowTask.Id == writerTask.Id
                ? JsonSerializer.Serialize(new
                {
                    automaticRewrite = true,
                    rewriteAttempt = failedTask.RetryCount,
                    requestedBy = "qa-gate",
                    reason = failureReason,
                    queuedUtc = now,
                    reworkDirective = reworkDirective != null ? JsonSerializer.Serialize(reworkDirective with { AttemptNumber = failedTask.RetryCount }, JsonOptions) : (string?)null
                }, JsonOptions)
                : "{}";
        }

        var job = await _dbContext.ContentWorkflowJobs
            .FirstOrDefaultAsync(j => j.Id == failedTask.ContentWorkflowJobId, cancellationToken);

        if (job is not null)
        {
            job.Status = "InProgress";
            job.CurrentStage = writerTask.StageName;
            job.ErrorMessage = $"QA requested automatic rewrite attempt {failedTask.RetryCount} of {maxAutomaticRewriteAttempts}.";
            job.LastUpdatedUtc = now;
        }

        _logger.LogInformation(
            "Queued automatic rewrite attempt {Attempt} for job {JobId} after QA failure.",
            failedTask.RetryCount,
            failedTask.ContentWorkflowJobId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? ExtractReworkDirectiveJson(string? inputDataJson)
    {
        if (string.IsNullOrWhiteSpace(inputDataJson) || inputDataJson == "{}") return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(inputDataJson);
            if (doc.RootElement.TryGetProperty("reworkDirective", out var element))
                return element.GetRawText();
        }
        catch { /* ignore */ }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(inputDataJson);
            if (doc.RootElement.TryGetProperty("reworkStrategy", out _))
                return inputDataJson;
        }
        catch { /* ignore */ }
        return null;
    }

    private async Task ExecuteTaskAsync(ContentWorkflowTask task, CancellationToken ct)
    {
        _logger.LogInformation("Executing {TaskName} using agent {Agent}...", task.Name, task.AssignedAgent);

        try
        {
            await Task.Delay(500, ct);

            var job = await _dbContext.ContentWorkflowJobs
                .FirstOrDefaultAsync(j => j.Id == task.ContentWorkflowJobId, ct);

            var idea = job is null
                ? null
                : await _dbContext.ContentIdeas.FirstOrDefaultAsync(i => i.Id == job.ContentIdeaId, ct);

            var article = await ResolveArticleAsync(task, idea, ct);
            LogArticleCheckpoint(task, idea, "resolved-input", article, null, null);

            if (!CanTaskSucceed(task, article))
            {
                throw new InvalidOperationException(BuildTaskFailureReason(task, article));
            }

            var dispatchResult = await _taskDispatcher.DispatchAsync(task, idea, article, ct);
            var artifact = dispatchResult.Artifact;

        task.OutputDataJson = artifact.ContentJson;
        task.Status = ContentOS.Domain.Enums.TaskStatus.Completed;
        task.ResultArtifactId = dispatchResult.PrimaryArtifactId.ToString();
        task.InputDataJson = JsonSerializer.Serialize(new
        {
            artifactId = artifact.Id,
            artifactType = artifact.ArtifactType,
            artifactTitle = artifact.Title,
            resolvedExecutionKey = dispatchResult.ResolvedExecutionKey,
            executionSummary = dispatchResult.ExecutionSummary,
            warnings = dispatchResult.Warnings,
            previewArtifactId = dispatchResult.PreviewArtifactId,
            supplementalArtifactIds = dispatchResult.SupplementalArtifactIds
        }, JsonOptions);
        task.CompletedUtc = DateTime.UtcNow;
        task.LastUpdatedUtc = DateTime.UtcNow;
        task.ErrorMessage = string.Empty;

        _dbContext.ContentArtifacts.Add(artifact);

        foreach (var generatedArtifact in dispatchResult.SupplementalArtifacts ?? Array.Empty<ContentArtifact>())
        {
            _dbContext.ContentArtifacts.Add(generatedArtifact);
        }

        var finalArticlePreviewArtifact = dispatchResult.FinalArticlePreviewArtifact;
        if (finalArticlePreviewArtifact is not null)
        {
            _dbContext.ContentArtifacts.Add(finalArticlePreviewArtifact);
        }

        if (job != null)
        {
            job.CurrentStage = task.StageName;
            job.LastUpdatedUtc = DateTime.UtcNow;
        }

        var nextTask = await _dbContext.ContentWorkflowTasks
            .Where(t => t.ContentWorkflowJobId == task.ContentWorkflowJobId && t.DisplayOrder > task.DisplayOrder && t.Status == ContentOS.Domain.Enums.TaskStatus.Pending)
            .OrderBy(t => t.DisplayOrder)
            .FirstOrDefaultAsync(ct);

        if (nextTask != null)
        {
            nextTask.Status = ContentOS.Domain.Enums.TaskStatus.Ready;
            nextTask.LastUpdatedUtc = DateTime.UtcNow;

            if (job != null)
            {
                job.CurrentStage = nextTask.StageName;
                job.Status = "InProgress";
            }
        }
        else if (job != null)
        {
            job.Status = "Completed";
            job.CompletedUtc = DateTime.UtcNow;
            job.CurrentStage = "Completed";
        }

        await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    internal async Task<IEnumerable<ContentArtifact>> BuildSupplementalArtifactsForDispatchAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken cancellationToken)
    {
        if (idea is null)
        {
            return Enumerable.Empty<ContentArtifact>();
        }

        var supplementalArtifacts = new List<ContentArtifact>();

        if (task.Name == "Research Topic and Intent")
        {
            var benchmark = await _serpBenchmarkService.BuildAsync(idea, cancellationToken);
            if (benchmark.ResultCount > 0)
            {
                supplementalArtifacts.Add(new ContentArtifact
                {
                    Id = Guid.NewGuid(),
                    ContentWorkflowJobId = task.ContentWorkflowJobId,
                    ContentWorkflowTaskId = task.Id,
                    ArtifactType = "SerpBenchmark",
                    Title = $"SERP benchmark for {idea.PrimaryKeyword}",
                    StorageType = "Database",
                    ContentJson = JsonSerializer.Serialize(new
                    {
                        query = benchmark.Query,
                        resultCount = benchmark.ResultCount,
                        averageWordCount = benchmark.AverageWordCount,
                        averagePrimaryKeywordCount = benchmark.AveragePrimaryKeywordCount,
                        averageHeadingCount = benchmark.AverageHeadingCount,
                        averageRelatedKeywordHits = benchmark.AverageRelatedKeywordHits,
                        recommendedWordCountMin = benchmark.RecommendedWordCountMin,
                        recommendedWordCountMax = benchmark.RecommendedWordCountMax,
                        recommendedPrimaryKeywordMin = benchmark.RecommendedPrimaryKeywordMin,
                        recommendedPrimaryKeywordMax = benchmark.RecommendedPrimaryKeywordMax,
                        relatedKeywords = benchmark.RelatedKeywords,
                        results = benchmark.Results
                    }, JsonOptions),
                    ContentText = $"SERP benchmark built from {benchmark.ResultCount} top-ranking results for {benchmark.Query}.",
                    VersionNumber = 1,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedByAgent = task.AssignedAgent
                });
            }
        }

        if (task.Name != "Humanize Final Draft")
        {
            if (task.Name == "Apply SEO and Linking")
            {
                var benchmarkArtifact = await _dbContext.ContentArtifacts
                    .Where(x => x.ContentWorkflowJobId == task.ContentWorkflowJobId && x.ArtifactType == "SerpBenchmark")
                    .OrderByDescending(x => x.CreatedUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (benchmarkArtifact is not null)
                {
                    var benchmark = DeserializeSerpBenchmark(benchmarkArtifact.ContentJson);
                    if (benchmark is not null)
                    {
                        var report = _optimizationScoringService.Compute(idea, article, benchmark);
                        supplementalArtifacts.Add(new ContentArtifact
                        {
                            Id = Guid.NewGuid(),
                            ContentWorkflowJobId = task.ContentWorkflowJobId,
                            ContentWorkflowTaskId = task.Id,
                            ArtifactType = "OptimizationScoreReport",
                            Title = $"Optimization score for {idea.Title}",
                            StorageType = "Database",
                            ContentJson = JsonSerializer.Serialize(report, JsonOptions),
                            ContentText = $"Optimization score {report.OptimizationScore}/100 ({report.StatusLabel}).",
                            VersionNumber = 1,
                            CreatedUtc = DateTime.UtcNow,
                            CreatedByAgent = task.AssignedAgent
                        });
                    }
                }

                // Save SeoValidationChecklist as its own artifact for easy access
                var seoArtifact = await _dbContext.ContentArtifacts
                    .Where(x => x.ContentWorkflowJobId == task.ContentWorkflowJobId && x.ArtifactType == "SeoOptimization")
                    .OrderByDescending(x => x.CreatedUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (seoArtifact is not null)
                {
                    try
                    {
                        var seoResult = JsonSerializer.Deserialize<SeoOptimizationResult>(seoArtifact.ContentJson, JsonOptionsWithCaseInsensitive);
                        if (seoResult?.ValidationChecklist is not null)
                        {
                            supplementalArtifacts.Add(new ContentArtifact
                            {
                                Id = Guid.NewGuid(),
                                ContentWorkflowJobId = task.ContentWorkflowJobId,
                                ContentWorkflowTaskId = task.Id,
                                ArtifactType = "SeoValidationChecklist",
                                Title = $"SEO Validation Checklist for {idea.Title}",
                                StorageType = "Database",
                                ContentJson = JsonSerializer.Serialize(seoResult.ValidationChecklist, JsonOptions),
                                ContentText = FormatChecklistSummary(seoResult.ValidationChecklist),
                                VersionNumber = 1,
                                CreatedUtc = DateTime.UtcNow,
                                CreatedByAgent = task.AssignedAgent
                            });
                        }
                    }
                    catch { /* proceed without checklist artifact */ }
                }
            }

            return supplementalArtifacts;
        }

        var title = idea.Title;
        var keyword = Fallback(idea.PrimaryKeyword, title);
        var angle = Fallback(idea.RecommendedAngle, $"A practical explanation of {keyword}");

        var assets = new[]
        {
            new
            {
                ArtifactType = "FeaturedImageAsset",
                Title = $"Featured image for {title}",
                ImageRole = "FeaturedHero",
                AspectRatio = "16:9",
                Width = 1536,
                Height = 1024,
                Prompt = $"Create a clean editorial hero image for '{title}' in a modern budgeting blog style. Focus on clarity, trust, and simple financial organization. No text, no words, no letters, no typography, no labels, no captions, no watermark, no logo, no numbers.",
                AltText = $"Featured illustration for {title}",
                Status = "PromptReady",
                SuggestedPlacement = "Top of article (Hero)"
            },
            new
            {
                ArtifactType = "SupportingImageAsset",
                Title = $"Supporting diagram for {title}",
                ImageRole = "SupportingDiagram",
                AspectRatio = "4:3",
                Width = 1280,
                Height = 960,
                Prompt = $"Create a supporting editorial diagram that explains '{angle}' for an article about '{keyword}'. Clean layout, simple shapes, neutral background. No text, no words, no letters, no typography, no labels, no captions, no watermark, no logo, no numbers.",
                AltText = $"Supporting diagram for {title}",
                Status = "PromptReady",
                SuggestedPlacement = "Inside section explaining the core concept"
            },
            new
            {
                ArtifactType = "SupportingImageAsset",
                Title = $"Checklist visual for {title}",
                ImageRole = "ChecklistGraphic",
                AspectRatio = "1:1",
                Width = 1024,
                Height = 1024,
                Prompt = $"Create a checklist-style supporting visual for '{keyword}' with a modern editorial look, minimal iconography, and a clear structured composition. No text, no words, no letters, no typography, no labels, no captions, no watermark, no logo, no numbers.",
                AltText = $"Checklist visual for {title}",
                Status = "PromptReady",
                SuggestedPlacement = "Near the actionable steps or checklist section"
            }
        };

        foreach (var asset in assets)
        {
            var imagePath = await _imageGenerationService.GenerateImageAsync(
                asset.Prompt, 
                asset.Width, 
                asset.Height, 
                cancellationToken: cancellationToken);

            supplementalArtifacts.Add(new ContentArtifact
            {
                Id = Guid.NewGuid(),
                ContentWorkflowJobId = task.ContentWorkflowJobId,
                ContentWorkflowTaskId = task.Id,
                ArtifactType = asset.ArtifactType,
                Title = asset.Title,
                StorageType = "Database",
                ContentJson = JsonSerializer.Serialize(new
                {
                    title = asset.Title,
                    imageRole = asset.ImageRole,
                    status = "Generated",
                    prompt = asset.Prompt,
                    altText = asset.AltText,
                    aspectRatio = asset.AspectRatio,
                    width = asset.Width,
                    height = asset.Height,
                    sourceTask = task.Name,
                    generated = true,
                    imagePath = imagePath,
                    suggestedPlacement = asset.SuggestedPlacement
                }, JsonOptions),
                ContentText = asset.Prompt,
                VersionNumber = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedByAgent = task.AssignedAgent
            });
        }

        return supplementalArtifacts;
    }

    private static SerpBenchmarkReport? DeserializeSerpBenchmark(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SerpBenchmarkReport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    internal async Task<GeneratedLongformArticle> ResolveArticleAsync(ContentWorkflowTask task, ContentIdea? idea, CancellationToken ct)
    {
        if (idea is null)
        {
            return BuildLongformArticle(null, new List<string>(), new List<string>());
        }

        var secondaryKeywords = GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea);
        var sourceSummaries = ParseStringArray(idea.SourceSummaryJson);

        var existingDraft = await TryLoadExistingDraftAsync(task.ContentWorkflowJobId, idea, ct);
        if (existingDraft is not null)
        {
            return EnrichArticleWithIdea(existingDraft, idea, secondaryKeywords, sourceSummaries);
        }

        if (task.Name is "Write Rule-Compliant Draft" or "Draft")
        {
            var targetRange = GetWordTargetRange(NormalizeContentType(idea.ContentType));

            // Load OptimizedSeoPackage if available (from the new pre-write optimization stage)
            OptimizedSeoPackage? seoPackage = null;
            try
            {
                var seoPackageArtifact = await _dbContext.ContentArtifacts
                    .Where(x => x.ContentWorkflowJobId == task.ContentWorkflowJobId && x.ArtifactType == "OptimizedSeoPackage")
                    .OrderByDescending(x => x.CreatedUtc)
                    .FirstOrDefaultAsync(ct);

                if (seoPackageArtifact is not null && !string.IsNullOrWhiteSpace(seoPackageArtifact.ContentJson))
                    seoPackage = JsonSerializer.Deserialize<OptimizedSeoPackage>(seoPackageArtifact.ContentJson, JsonOptionsWithCaseInsensitive);
            }
            catch { /* proceed without */ }

            // Use optimized title/keywords if available
            var writeTitle = seoPackage?.BestTitle ?? idea.Title;
            var writeMeta = seoPackage?.BestMetaDescription ?? idea.Summary;
            var writeSecondaryKeywords = seoPackage != null
                ? seoPackage.SecondaryKeywords.Concat(seoPackage.SemanticKeywords).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : secondaryKeywords;

            // Enrich secondary keywords with FAQ keywords from optimized package
            if (seoPackage?.FaqKeywords is { Length: > 0 })
            {
                foreach (var faqKw in seoPackage.FaqKeywords)
                {
                    if (!writeSecondaryKeywords.Contains(faqKw, StringComparer.OrdinalIgnoreCase))
                        writeSecondaryKeywords.Add(faqKw);
                }
            }

            // Extract rework directive from task input (if this is a QA-triggered rewrite)
            string? reworkDirectiveJson = null;
            if (!string.IsNullOrWhiteSpace(task.InputDataJson) && task.InputDataJson != "{}")
            {
                try
                {
                    using var inputDoc = System.Text.Json.JsonDocument.Parse(task.InputDataJson);
                    if (inputDoc.RootElement.TryGetProperty("reworkStrategy", out _))
                    {
                        reworkDirectiveJson = task.InputDataJson;
                    }
                }
                catch { /* not JSON or no rework directive — first write */ }
            }

            // Load Topic Expansion and Outline if available
            TopicExpansionResult? topicExpansion = null;
            ArticleOutlineResult? articleOutline = null;
            try
            {
                topicExpansion = await LoadTopicExpansionAsync(task.ContentWorkflowJobId);
                
                var outlineArtifact = await _dbContext.ContentArtifacts
                    .Where(x => x.ContentWorkflowJobId == task.ContentWorkflowJobId && x.ArtifactType == "ArticleOutline")
                    .OrderByDescending(x => x.CreatedUtc)
                    .FirstOrDefaultAsync(ct);

                if (outlineArtifact is not null && !string.IsNullOrWhiteSpace(outlineArtifact.ContentJson))
                    articleOutline = JsonSerializer.Deserialize<ArticleOutlineResult>(outlineArtifact.ContentJson, JsonOptionsWithCaseInsensitive);
            }
            catch { /* proceed without */ }

            var generatedDraft = await _workflowArticleWriter.GenerateDraftAsync(
                NormalizeContentType(idea.ContentType),
                writeTitle,
                idea.SlugSuggestion,
                idea.PrimaryKeyword,
                writeMeta,
                idea.SearchIntent,
                idea.AudiencePainPoint,
                idea.AudienceGoal,
                idea.RecommendedAngle,
                idea.WhyNow,
                writeSecondaryKeywords,
                sourceSummaries,
                targetRange.Min,
                targetRange.Max,
                topicExpansion,
                articleOutline,
                reworkDirectiveJson,
                ct);

            if (generatedDraft is not null)
            {
                _logger.LogInformation("Generated real workflow draft for job {JobId} with model-backed writer.", task.ContentWorkflowJobId);
                var enrichedDraft = EnrichArticleWithIdea(ToGeneratedLongformArticle(generatedDraft, idea), idea, secondaryKeywords, sourceSummaries);
                return await EnhanceArticlePresentationAsync(task.ContentWorkflowJobId, enrichedDraft, sourceSummaries, ct);
            }

            _logger.LogWarning("Falling back to synthetic workflow article draft for job {JobId}.", task.ContentWorkflowJobId);
        }

        var fallbackArticle = BuildLongformArticle(idea, secondaryKeywords, sourceSummaries);
        return await EnhanceArticlePresentationAsync(task.ContentWorkflowJobId, fallbackArticle, sourceSummaries, ct);
    }

    private async Task<GeneratedLongformArticle?> TryLoadExistingDraftAsync(Guid jobId, ContentIdea? idea, CancellationToken ct)
    {
        var draftArtifact = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == jobId && x.ArtifactType == "DraftArticle")
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (draftArtifact is null)
        {
            return null;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<WorkflowArticleDraft>(draftArtifact.ContentJson, JsonOptionsWithCaseInsensitive);
            return draft is null ? null : ToGeneratedLongformArticle(draft, idea);
        }
        catch (Exception ex)
        {
            LogSafeFailure(ex, "The saved article draft could not be read; any diagnostic template remains unpublishable.", jobId);
            return null;
        }
    }

    private static GeneratedLongformArticle ToGeneratedLongformArticle(WorkflowArticleDraft draft, ContentIdea? idea)
    {
        var contentType = NormalizeContentType(Fallback(draft.ContentType, idea?.ContentType ?? "LongFormBlogArticle"));
        var targetRange = GetWordTargetRange(contentType);
        var title = Fallback(draft.Title, idea?.Title ?? "Approved topic");
        var slug = Fallback(draft.Slug, idea?.SlugSuggestion ?? string.Empty);
        var summary = Fallback(draft.Summary, idea?.Summary ?? string.Empty);
        var introParagraphs = draft.IntroParagraphs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var sections = draft.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Heading) && section.Paragraphs.Any(paragraph => !string.IsNullOrWhiteSpace(paragraph)))
            .Select(section => new GeneratedSection(
                section.Heading.Trim(),
                section.Paragraphs.Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)).Select(paragraph => paragraph.Trim()).ToList()))
            .ToList();
        var conclusionParagraphs = draft.ConclusionParagraphs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var bodyText = string.Join("\n\n", sections.Select(section => $"## {section.Heading}\n\n{section.BodyText}"));
        var fullText = string.Join("\n\n", new[]
        {
            $"# {title}",
            summary,
            string.Join("\n\n", introParagraphs),
            bodyText,
            "## Conclusion",
            string.Join("\n\n", conclusionParagraphs),
            "## Next step",
            Fallback(draft.CallToAction, "Choose one next step and apply it today.")
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var estimatedWordCount = draft.EstimatedWordCount > 0 ? draft.EstimatedWordCount : CountWords(fullText);
        var estimatedReadTimeMinutes = draft.EstimatedReadTimeMinutes > 0 ? draft.EstimatedReadTimeMinutes : Math.Max(12, (int)Math.Ceiling(estimatedWordCount / 220m));

        return new GeneratedLongformArticle(
            title,
            slug,
            summary,
            Fallback(draft.MetaDescription, idea?.Summary ?? summary),
            draft.TargetWordCountMin > 0 ? draft.TargetWordCountMin : targetRange.Min,
            draft.TargetWordCountMax > 0 ? draft.TargetWordCountMax : targetRange.Max,
            estimatedWordCount,
            estimatedReadTimeMinutes,
            introParagraphs,
            sections,
            conclusionParagraphs,
            Fallback(draft.CallToAction, "Choose one next step and apply it today."),
            bodyText,
            fullText,
            false,
            introParagraphs.Count >= 3
                && sections.Count >= 8
                && estimatedWordCount >= Math.Max(targetRange.Min - 400, 1200)
                && sections.Any(section => section.Heading.Contains("FAQ", StringComparison.OrdinalIgnoreCase))
                && sections.Any(section => section.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(draft.CallToAction));
    }

    internal Task<object> BuildResearchIntentPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => Task.FromResult<object>(_contentStrategyAgent.BuildResearchIntentAsync(idea!, ParseStringArray(idea?.SourceSummaryJson), ct).Result);

    internal Task<object> BuildKeywordStrategyPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
    {
        var payload = _contentStrategyAgent.BuildKeywordStrategyAsync(idea!, GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea!), ct).Result;
        _logger.LogInformation("Keyword strategy retained a main phrase and {RelatedPhraseCount} related phrases, including {QuestionCount} supplied questions. Ranking opportunity is not measured here. WorkflowRunId={WorkflowRunId} TaskId={TaskId}", payload.SecondaryKeywords.Length, payload.FaqQuestions.Length, task.ContentWorkflowJobId, task.Id);
        return Task.FromResult<object>(payload);
    }

    internal async Task<object> BuildTopicExpansionPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
    {
        var secondaryKeywords = GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea!);
        var sourceSummaries = ParseStringArray(idea?.SourceSummaryJson);
        return await _contentStrategyAgent.BuildTopicExpansionAsync(idea!, await LoadKeywordStrategyAsync(task.ContentWorkflowJobId, idea!, secondaryKeywords), sourceSummaries, ct);
    }

    internal Task<object> BuildOptimizedSeoPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => Task.FromResult<object>(BuildOptimizedSeoPackageAsync(idea!, GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea!)).Result);

    internal async Task<object> BuildStructuredOutlinePayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => await _contentStrategyAgent.CreateArticleOutlineAsync(idea!, article, await LoadTopicExpansionAsync(task.ContentWorkflowJobId), ct);

    internal Task<object> BuildDraftPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
    {
        LogArticleCheckpoint(task, idea, "after-write-draft", article, null, null);
        return Task.FromResult<object>(new WorkflowArticleDraft
        {
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            MetaDescription = article.MetaDescription,
            ContentType = idea?.ContentType ?? "LongFormBlogArticle",
            TargetWordCountMin = article.TargetWordCountMin,
            TargetWordCountMax = article.TargetWordCountMax,
            EstimatedWordCount = article.EstimatedWordCount,
            EstimatedReadTimeMinutes = article.EstimatedReadTimeMinutes,
            IntroParagraphs = article.IntroParagraphs.ToList(),
            Sections = article.Sections.Select(section => new WorkflowArticleSectionDraft
            {
                Heading = section.Heading,
                Paragraphs = section.Paragraphs.ToList()
            }).ToList(),
            ConclusionParagraphs = article.ConclusionParagraphs.ToList(),
            CallToAction = article.CallToAction
        });
    }

    internal async Task<object> BuildSeoLinkingPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => await _seoAndMonetizationAgent.ApplyOnPageSeoAsync(article, GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea!), await LoadOptimizedSeoPackageJsonAsync(task.ContentWorkflowJobId));

    internal Task<object> BuildMonetizationPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => Task.FromResult<object>(_seoAndMonetizationAgent.AddCtaAndMonetizationPlacementsAsync(article).Result);

    internal async Task<object> BuildStrictQaPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
    {
        var normalizedArticle = NormalizeSeoPhrasing(article, idea);
        var validation = ValidateSeoPhrasing(normalizedArticle, idea);
        LogArticleCheckpoint(task, idea, "before-qa", normalizedArticle, validation, null);

        if (!validation.IsValid)
        {
            var rehumanizedArticle = await TryRehumanizeForSeoValidationAsync(task, idea, normalizedArticle, validation, ct);
            var secondValidation = ValidateSeoPhrasing(rehumanizedArticle, idea);
            if (!secondValidation.IsValid)
            {
                _logger.LogWarning("Article wording checks still failed after adjustment. Review the named checks before retrying. WorkflowRunId={WorkflowRunId} ValidationCodes={ValidationCodes}", task.ContentWorkflowJobId, WorkflowDiagnostics.ValidationCodes(string.Join("; ", secondValidation.Failures)));
                throw new InvalidOperationException($"SEO phrasing gate failed before QA: {string.Join("; ", secondValidation.Failures)}");
            }

            normalizedArticle = rehumanizedArticle;
        }

        return await _qaAndComplianceAgent.RunFinalQaReviewAsync(idea, normalizedArticle, GetEffectiveSecondaryKeywords(task.ContentWorkflowJobId, idea!), ParseStringArray(idea?.SourceSummaryJson), ct);
    }

    internal Task<object> BuildHumanizedPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
    {
        var normalizedArticle = NormalizeSeoPhrasing(article, idea);
        LogArticleCheckpoint(task, idea, "after-humanize", normalizedArticle, null, null);
        return Task.FromResult<object>(new
        {
            humanizer = _editorialAgent.HumanizeAndImproveReadabilityAsync(normalizedArticle, ct).Result,
            headlinePack = _editorialAgent.ImproveHeadlineAndHookAsync(normalizedArticle, idea?.PrimaryKeyword ?? string.Empty, ct).Result,
            publishReady = true,
            title = normalizedArticle.Title,
            slug = normalizedArticle.Slug,
            summary = normalizedArticle.Summary,
            metaDescription = normalizedArticle.MetaDescription,
            estimatedWordCount = normalizedArticle.EstimatedWordCount,
            htmlBody = BuildArticleHtmlBody(normalizedArticle, []),
            bodyText = normalizedArticle.BodyText,
            callToAction = normalizedArticle.CallToAction
        });
    }

    internal Task<object> BuildLightQaPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => Task.FromResult<object>(_lightQaAgent.RunLightQaCheckAsync(idea, article, ct).Result);

    internal Task<object> BuildDefaultPayloadAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, CancellationToken ct)
        => Task.FromResult<object>(new { result = $"Simulated output for {task.Name}.", agent = task.AssignedAgent });

    internal ContentArtifact CreateArtifactForDispatch(ContentWorkflowTask task, object payload, string artifactType)
    {
        var artifact = new ContentArtifact
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = task.ContentWorkflowJobId,
            ContentWorkflowTaskId = task.Id,
            ArtifactType = artifactType,
            Title = task.Name,
            StorageType = "Database",
            ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
            ContentText = BuildArtifactText(artifactType, payload),
            VersionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedByAgent = task.AssignedAgent
        };

        _logger.LogInformation("Stage output was saved locally. WorkflowRunId={WorkflowRunId} TaskId={TaskId} ArtifactType={ArtifactType} ArtifactId={ArtifactId}", task.ContentWorkflowJobId, task.Id, WorkflowDiagnostics.Identifier(artifact.ArtifactType), artifact.Id);

        return artifact;
    }

    private async Task<TopicExpansionResult> LoadTopicExpansionAsync(Guid jobId)
    {
        try
        {
            var artifact = await _dbContext.ContentArtifacts
                .Where(x => x.ContentWorkflowJobId == jobId && x.ArtifactType == "TopicExpansion")
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync();

            if (artifact is not null)
            {
                var result = JsonSerializer.Deserialize<TopicExpansionResult>(artifact.ContentJson, JsonOptionsWithCaseInsensitive);
                return result ?? throw new InvalidOperationException("TopicExpansion artifact JSON was empty.");
            }
        }
        catch (Exception ex)
        {
            LogSafeFailure(ex, "The saved topic map could not be read. Default coverage suggestions will be used, not claimed as research.", jobId);
        }

        return new TopicExpansionResult(
            "Default coverage: provide comprehensive, high-value guidance.",
            new[] { "Introduction", "Key Concepts", "Action Steps", "Common Mistakes", "FAQ" },
            new[] { "Practical examples", "Real-world numbers", "Direct action steps" },
            new string[0],
            new string[0],
            false);
    }

    private async Task<KeywordStrategyResult> LoadKeywordStrategyAsync(Guid jobId, ContentIdea idea, IReadOnlyCollection<string> secondaryKeywords)
    {
        try
        {
            var artifact = await _dbContext.ContentArtifacts
                .Where(x => x.ContentWorkflowJobId == jobId && x.ArtifactType == "KeywordStrategy")
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync();

            if (artifact is not null)
            {
                var strategy = JsonSerializer.Deserialize<KeywordStrategyResult>(artifact.ContentJson, JsonOptionsWithCaseInsensitive);
                if (strategy is not null) return strategy;
            }
        }
        catch { }

        return await _contentStrategyAgent.BuildKeywordStrategyAsync(idea, secondaryKeywords);
    }

    private async Task<string?> LoadOptimizedSeoPackageJsonAsync(Guid jobId)
    {
        try
        {
            var artifact = await _dbContext.ContentArtifacts
                .Where(x => x.ContentWorkflowJobId == jobId && x.ArtifactType == "OptimizedSeoPackage")
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync();

            return artifact?.ContentJson;
        }
        catch { return null; }
    }

    private async Task<object> BuildOptimizedSeoPackageAsync(ContentIdea idea, IReadOnlyCollection<string> secondaryKeywords)
    {
        // Load keyword strategy from previous task artifact
        var keywordArtifact = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId != Guid.Empty)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(x => x.ArtifactType == "KeywordStrategy");

        KeywordStrategyResult? keywordStrategy = null;
        if (keywordArtifact != null)
        {
            try { keywordStrategy = JsonSerializer.Deserialize<KeywordStrategyResult>(keywordArtifact.ContentJson, JsonOptionsWithCaseInsensitive); }
            catch { /* proceed without */ }
        }

        keywordStrategy ??= new KeywordStrategyResult(
            idea.PrimaryKeyword,
            secondaryKeywords.Select(k => k.Trim()).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray(),
            secondaryKeywords.Select(k => k.Trim()).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            [idea.Title],
            [],
            "Informational",
            true);

        return await _seoOptimizationAgent.BuildOptimizedSeoPackageAsync(idea, keywordStrategy, secondaryKeywords);
    }

    private static GeneratedLongformArticle EnrichArticleWithIdea(
        GeneratedLongformArticle article,
        ContentIdea? idea,
        IReadOnlyCollection<string> secondaryKeywords,
        IReadOnlyCollection<string> sourceSummaries)
    {
        if (idea is null)
        {
            return article;
        }

        var slug = string.IsNullOrWhiteSpace(article.Slug) ? idea.SlugSuggestion : article.Slug;
        var summary = string.IsNullOrWhiteSpace(article.Summary) || article.Summary.StartsWith("A practical longform article about ", StringComparison.OrdinalIgnoreCase)
            ? Fallback(idea.Summary, article.Summary)
            : article.Summary;
        var metaDescription = string.IsNullOrWhiteSpace(article.MetaDescription) || article.MetaDescription.Contains("focused on .", StringComparison.Ordinal)
            ? BuildMetaDescription(article.Title, idea.PrimaryKeyword, idea.AudienceGoal, article.CallToAction)
            : article.MetaDescription;
        var fallbackArticle = BuildLongformArticle(idea, secondaryKeywords.ToList(), sourceSummaries.ToList());
        var introParagraphs = article.IntroParagraphs.Count > 0 ? article.IntroParagraphs : fallbackArticle.IntroParagraphs;
        var conclusionParagraphs = article.ConclusionParagraphs.Count > 0 ? article.ConclusionParagraphs : fallbackArticle.ConclusionParagraphs;
        var normalizedSections = EnsureScannableSections(article.Sections, fallbackArticle.Sections);
        var bodyText = string.Join("\n\n", normalizedSections.Select(section => $"## {section.Heading}\n\n{section.BodyText}"));
        var fullText = string.Join("\n\n", new[]
        {
            $"# {article.Title}",
            summary,
            string.Join("\n\n", introParagraphs),
            bodyText,
            "## Conclusion",
            string.Join("\n\n", conclusionParagraphs),
            "## Next step",
            article.CallToAction
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return article with
        {
            Slug = slug,
            Summary = summary,
            MetaDescription = metaDescription,
            IntroParagraphs = introParagraphs,
            Sections = normalizedSections,
            BodyText = bodyText,
            ConclusionParagraphs = conclusionParagraphs,
            FullText = fullText
        };
    }

    private List<string> GetEffectiveSecondaryKeywords(Guid jobId, ContentIdea idea)
    {
        var keywords = ParseStringArray(idea.SecondaryKeywordsJson)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count >= 3)
        {
            return keywords;
        }

        var keywordArtifact = _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == jobId && x.ArtifactType == "KeywordStrategy")
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefault();

        if (keywordArtifact is null || string.IsNullOrWhiteSpace(keywordArtifact.ContentJson))
        {
            return keywords;
        }

        try
        {
            var strategy = JsonSerializer.Deserialize<KeywordStrategyResult>(keywordArtifact.ContentJson, JsonOptionsWithCaseInsensitive);
            if (strategy?.SecondaryKeywords is null)
            {
                return keywords;
            }

            return strategy.SecondaryKeywords
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Concat(keywords)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return keywords;
        }
    }

    private static List<GeneratedSection> EnsureScannableSections(List<GeneratedSection> sections, List<GeneratedSection> fallbackSections)
    {
        if (sections.Count == 0)
        {
            return fallbackSections;
        }

        return sections.Select(section =>
        {
            var fallbackSection = fallbackSections.FirstOrDefault(x => string.Equals(x.Heading, section.Heading, StringComparison.OrdinalIgnoreCase));
            var paragraphs = section.Paragraphs.Count > 0
                ? section.Paragraphs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
                : fallbackSection?.Paragraphs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? new List<string>();

            if (paragraphs.Count == 0)
            {
                return section;
            }

            if (section.Heading.Contains("What You'll Learn", StringComparison.OrdinalIgnoreCase) || section.Heading.Contains("What You’ll Learn", StringComparison.OrdinalIgnoreCase))
            {
                paragraphs = paragraphs.Select(AddBulletPrefix).ToList();
            }
            else if (section.Heading.Contains("step-by-step", StringComparison.OrdinalIgnoreCase) || section.Heading.Contains("step by step", StringComparison.OrdinalIgnoreCase))
            {
                if (!paragraphs.Any(IsNumberedLine))
                {
                    paragraphs = paragraphs.Select((value, index) => $"{index + 1}. {TrimListPrefix(value)}").ToList();
                }
            }
            else if (section.Heading.Contains("Tool", StringComparison.OrdinalIgnoreCase))
            {
                paragraphs = paragraphs.Select(AddBulletPrefix).ToList();
            }

            return new GeneratedSection(section.Heading, paragraphs);
        }).ToList();
    }

    private static string AddBulletPrefix(string value)
    {
        var trimmed = TrimListPrefix(value);
        return trimmed.StartsWith("- ", StringComparison.Ordinal) ? trimmed : $"- {trimmed}";
    }

    private static bool IsNumberedLine(string value)
        => Regex.IsMatch(value.TrimStart(), "^\\d+\\.\\s");

    private static string TrimListPrefix(string value)
        => Regex.Replace(value.Trim(), "^(-|\\d+\\.)\\s*", string.Empty);

    private static string BuildMetaDescription(string title, string? primaryKeyword, string? audienceGoal, string? callToAction)
    {
        var keyword = Fallback(primaryKeyword, title);
        var goal = string.IsNullOrWhiteSpace(audienceGoal) ? "take a practical next step" : audienceGoal.Trim().TrimEnd('.');
        return $"{title} helps readers use {keyword} to {goal.ToLowerInvariant()} with a practical, clear approach.";
    }

    internal ContentArtifact? BuildFinalArticlePreviewArtifactForDispatch(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article)
    {
        if (task.Name != "Humanize Final Draft" || idea is null)
        {
            return null;
        }

        var normalizedArticle = NormalizeSeoPhrasing(article, idea);
        LogArticleCheckpoint(task, idea, "final-preview-source", normalizedArticle, ValidateSeoPhrasing(normalizedArticle, idea), null);
        var keywordStrength = BuildKeywordStrength(idea);

        var payload = new
        {
            title = normalizedArticle.Title,
            slug = normalizedArticle.Slug,
            primaryKeyword = idea.PrimaryKeyword,
            keywordStrengthLabel = keywordStrength.Label,
            keywordStrengthScore = keywordStrength.Score,
            keywordStrengthReason = keywordStrength.Reason,
            contentType = idea.ContentType,
            summary = normalizedArticle.Summary,
            metaDescription = normalizedArticle.MetaDescription,
            targetWordCountMin = normalizedArticle.TargetWordCountMin,
            targetWordCountMax = normalizedArticle.TargetWordCountMax,
            estimatedWordCount = normalizedArticle.EstimatedWordCount,
            estimatedReadTimeMinutes = normalizedArticle.EstimatedReadTimeMinutes,
            intro = string.Join("\n\n", normalizedArticle.IntroParagraphs),
            introParagraphs = normalizedArticle.IntroParagraphs,
            sectionOneHeading = normalizedArticle.Sections.ElementAtOrDefault(0)?.Heading,
            sectionOneBody = normalizedArticle.Sections.ElementAtOrDefault(0)?.BodyText,
            sectionTwoHeading = normalizedArticle.Sections.ElementAtOrDefault(1)?.Heading,
            sectionTwoBody = normalizedArticle.Sections.ElementAtOrDefault(1)?.BodyText,
            sectionThreeHeading = normalizedArticle.Sections.ElementAtOrDefault(2)?.Heading,
            sectionThreeBody = normalizedArticle.Sections.ElementAtOrDefault(2)?.BodyText,
            sectionFourHeading = normalizedArticle.Sections.ElementAtOrDefault(3)?.Heading,
            sectionFourBody = normalizedArticle.Sections.ElementAtOrDefault(3)?.BodyText,
            sections = normalizedArticle.Sections.Select(section => new
            {
                heading = section.Heading,
                paragraphs = section.Paragraphs
            }).ToArray(),
            conclusion = string.Join("\n\n", normalizedArticle.ConclusionParagraphs),
            conclusionParagraphs = normalizedArticle.ConclusionParagraphs,
            callToAction = normalizedArticle.CallToAction,
            htmlBody = BuildArticleHtmlBody(normalizedArticle, [])
        };

        var artifact = new ContentArtifact
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = task.ContentWorkflowJobId,
            ContentWorkflowTaskId = task.Id,
            ArtifactType = "FinalArticlePreview",
            Title = normalizedArticle.Title,
            StorageType = "Database",
            ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
            ContentText = normalizedArticle.FullText,
            VersionNumber = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedByAgent = task.AssignedAgent
        };

        _logger.LogInformation("An article preview was saved locally; preview creation does not certify quality. WorkflowRunId={WorkflowRunId} TaskId={TaskId} ArtifactId={ArtifactId}", task.ContentWorkflowJobId, task.Id, artifact.Id);

        return artifact;
    }

    private async Task<GeneratedLongformArticle> TryRehumanizeForSeoValidationAsync(ContentWorkflowTask task, ContentIdea? idea, GeneratedLongformArticle article, SeoPhrasingValidationResult validation, CancellationToken ct)
    {
        if (idea is null)
        {
            return article;
        }

        var humanized = await _editorialAgent.HumanizeAndImproveReadabilityAsync(article, ct);

        var feedbackNote = string.Join("; ", validation.Failures);
        var sections = article.Sections
            .Select((section, index) =>
            {
                var heading = section.Heading;
                if (validation.HeadingFailureIndexes.Contains(index))
                {
                    heading = CleanHeading(BuildReaderFocusedHeadingVariant(heading, idea, index));
                }

                var paragraphs = section.Paragraphs
                    .Select(paragraph => ReduceExactPhraseRepetition(paragraph, idea.PrimaryKeyword, BuildKeywordVariations(idea, idea.PrimaryKeyword.Trim())))
                    .ToList();

                return new GeneratedSection(heading, paragraphs);
            })
            .ToList();

        var updatedArticle = article with
        {
            Sections = sections,
            BodyText = string.Join("\n\n", sections.Select(section => $"## {section.Heading}\n\n{section.BodyText}")),
            FullText = string.Join("\n\n", new[]
            {
                $"# {article.Title}",
                article.Summary,
                string.Join("\n\n", article.IntroParagraphs),
                string.Join("\n\n", sections.Select(section => $"## {section.Heading}\n\n{section.BodyText}")),
                "## Conclusion",
                string.Join("\n\n", article.ConclusionParagraphs),
                "## Next step",
                article.CallToAction
            }.Where(x => !string.IsNullOrWhiteSpace(x)))
        };

        _logger.LogInformation("Article wording was sent through one adjustment pass before QA. Review the named rules if it still fails. WorkflowRunId={WorkflowRunId} ValidationCodes={ValidationCodes}", task.ContentWorkflowJobId, WorkflowDiagnostics.ValidationCodes(feedbackNote));

        return NormalizeSeoPhrasing(updatedArticle, idea);
    }

    private static SeoPhrasingValidationResult ValidateSeoPhrasing(GeneratedLongformArticle article, ContentIdea? idea)
    {
        if (idea is null || string.IsNullOrWhiteSpace(idea.PrimaryKeyword))
        {
            return SeoPhrasingValidationResult.Valid();
        }

        var primaryKeyword = idea.PrimaryKeyword.Trim();
        var failures = new List<string>();
        var headingFailureIndexes = new List<int>();
        var headingKeywordCount = 0;

        for (var i = 0; i < article.Sections.Count; i++)
        {
            var heading = article.Sections[i].Heading ?? string.Empty;
            if (heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase))
            {
                headingKeywordCount++;
                if (headingKeywordCount > 1)
                {
                    headingFailureIndexes.Add(i);
                }
            }

            if (LooksLikeKeywordStuffedHeading(heading, primaryKeyword))
            {
                headingFailureIndexes.Add(i);
            }
        }

        if (headingKeywordCount > 1)
        {
            failures.Add($"exact primary keyword appears in {headingKeywordCount} headings");
        }

        if (headingFailureIndexes.Count > 0)
        {
            failures.Add("repeated keyword-stuffed headings detected");
        }

        var bodyKeywordCount = Regex.Matches(article.BodyText ?? string.Empty, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;
        if (bodyKeywordCount > 3)
        {
            failures.Add($"exact primary keyword appears {bodyKeywordCount} times in body text");
        }

        if (LooksLikeStitchedExactMatchPhrasing(article.BodyText, primaryKeyword))
        {
            failures.Add("obvious repeated stitched exact-match phrasing detected in body copy");
        }

        return failures.Count == 0
            ? SeoPhrasingValidationResult.Valid()
            : new SeoPhrasingValidationResult(false, failures, headingFailureIndexes.Distinct().ToArray());
    }

    private static bool LooksLikeKeywordStuffedHeading(string heading, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return false;
        }

        var lowered = heading.ToLowerInvariant();
        var loweredKeyword = primaryKeyword.ToLowerInvariant();
        return lowered.Contains(loweredKeyword)
            && (lowered.Replace(loweredKeyword, string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 2
                || lowered.Count(c => c == ':') > 1);
    }

    private static bool LooksLikeStitchedExactMatchPhrasing(string? bodyText, string primaryKeyword)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return false;
        }

        var matches = Regex.Matches(bodyText, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;
        return matches > 0 && Regex.IsMatch(bodyText, $"{Regex.Escape(primaryKeyword)}\\s+(for|with|and|2026|seasonal)", RegexOptions.IgnoreCase) && matches > 2;
    }

    private static string BuildReaderFocusedHeadingVariant(string originalHeading, ContentIdea idea, int index)
    {
        var replacements = BuildApprovedKeywordVariants(idea.PrimaryKeyword.Trim());
        var replacement = replacements.Count > 0 ? replacements[index % replacements.Count] : RemoveKeywordPhraseOnly(originalHeading, idea.PrimaryKeyword.Trim());
        return ReplaceExactPhraseOnce(originalHeading, idea.PrimaryKeyword.Trim(), replacement);
    }

    private static string ReduceExactPhraseRepetition(string paragraph, string primaryKeyword, List<string> variations)
    {
        if (string.IsNullOrWhiteSpace(paragraph) || string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return paragraph;
        }

        var matches = Regex.Matches(paragraph, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;
        if (matches <= 1)
        {
            return paragraph;
        }

        var kept = 0;
        var replacementIndex = 0;
        return Regex.Replace(paragraph, Regex.Escape(primaryKeyword), match =>
        {
            kept++;
            if (kept <= 2)
            {
                return match.Value;
            }

            var replacement = variations.Count > 0 ? variations[Math.Min(replacementIndex++, variations.Count - 1)] : primaryKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? match.Value;
            return replacement;
        }, RegexOptions.IgnoreCase);
    }

    private GeneratedLongformArticle NormalizeSeoPhrasing(GeneratedLongformArticle article, ContentIdea? idea)
    {
        if (idea is null || string.IsNullOrWhiteSpace(idea.PrimaryKeyword))
        {
            return article;
        }

        var primaryKeyword = idea.PrimaryKeyword.Trim();
        var approvedVariants = BuildApprovedKeywordVariants(primaryKeyword);
        if (approvedVariants.Count == 0)
        {
            approvedVariants.Add(BuildConservativeKeywordFallback(primaryKeyword));
        }
        var beforeHeadingCount = article.Sections.Count(section => section.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        var beforeBodyCount = Regex.Matches(article.BodyText ?? string.Empty, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;
        var replacementsUsed = new List<string>();

        var strongestHeadingIndex = article.Sections.FindIndex(section => section.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        var seenHeadingMatch = false;
        var normalizedSections = new List<GeneratedSection>();
        var variantIndex = 0;

        for (var i = 0; i < article.Sections.Count; i++)
        {
            var section = article.Sections[i];
            var heading = section.Heading;
            if (!string.IsNullOrWhiteSpace(heading) && heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase))
            {
                if (!seenHeadingMatch && i == (strongestHeadingIndex >= 0 ? strongestHeadingIndex : i))
                {
                    seenHeadingMatch = true;
                }
                else
                {
                    var replacement = approvedVariants[ClampVariantIndex(variantIndex, approvedVariants.Count)];
                    variantIndex++;
                    var updatedHeading = ReplaceFullPhraseVariant(heading, primaryKeyword, replacement);
                    if (!IsReadableHeading(updatedHeading))
                    {
                        updatedHeading = BuildSafeHeadingFallback(heading, replacement);
                    }

                    _logger.LogInformation("Adjusted a heading to reduce repeated keyword phrasing. HeadingCharactersBefore={HeadingCharactersBefore} HeadingCharactersAfter={HeadingCharactersAfter}", heading.Length, updatedHeading.Length);

                    heading = CleanHeading(updatedHeading);
                    replacementsUsed.Add($"heading:{replacement}");
                }
            }

            normalizedSections.Add(new GeneratedSection(heading, section.Paragraphs.ToList()));
        }

        var exactSeen = 0;
        variantIndex = 0;
        normalizedSections = normalizedSections.Select(section =>
        {
            var paragraphs = section.Paragraphs.Select(paragraph =>
            {
                var originalParagraph = paragraph;
                var updatedParagraph = Regex.Replace(paragraph, Regex.Escape(primaryKeyword), match =>
                {
                    exactSeen++;
                    if (exactSeen <= 2)
                    {
                        return match.Value;
                    }

                    var replacement = approvedVariants[ClampVariantIndex(variantIndex, approvedVariants.Count)];
                    variantIndex++;
                    replacementsUsed.Add($"body:{replacement}");
                    return replacement;
                }, RegexOptions.IgnoreCase);

                if (!string.Equals(originalParagraph, updatedParagraph, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Adjusted a paragraph to reduce repeated keyword phrasing. ParagraphCharactersBefore={ParagraphCharactersBefore} ParagraphCharactersAfter={ParagraphCharactersAfter}", originalParagraph.Length, updatedParagraph.Length);
                }

                return updatedParagraph;
            }).ToList();

            return new GeneratedSection(section.Heading, paragraphs);
        }).ToList();

        var normalizedBodyText = string.Join("\n\n", normalizedSections.Select(section => $"## {section.Heading}\n\n{section.BodyText}"));
        var normalizedFullText = string.Join("\n\n", new[]
        {
            $"# {article.Title}",
            article.Summary,
            string.Join("\n\n", article.IntroParagraphs),
            normalizedBodyText,
            "## Conclusion",
            string.Join("\n\n", article.ConclusionParagraphs),
            "## Next step",
            article.CallToAction
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var afterHeadingCount = normalizedSections.Count(section => section.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        var afterBodyCount = Regex.Matches(normalizedBodyText, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;

        _logger.LogInformation("Keyword phrasing adjustment finished: heading mentions changed from {HeadingBefore} to {HeadingAfter}, body mentions from {BodyBefore} to {BodyAfter}. ReplacementCount={ReplacementCount}", beforeHeadingCount, afterHeadingCount, beforeBodyCount, afterBodyCount, replacementsUsed.Count);

        return article with
        {
            Sections = normalizedSections,
            BodyText = normalizedBodyText,
            FullText = normalizedFullText
        };
    }

    private static List<string> BuildKeywordVariations(ContentIdea idea, string primaryKeyword)
        => BuildApprovedKeywordVariants(primaryKeyword);

    private static List<string> BuildApprovedKeywordVariants(string primaryKeyword)
    {
        var tokens = primaryKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var lowered = tokens.Select(t => t.ToLowerInvariant()).ToList();
        var variants = new List<string>();

        var seasonalFiltered = tokens.Where(t => !string.Equals(t, "seasonal", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(t, "^20\\d\\d$", RegexOptions.IgnoreCase)).ToList();
        if (seasonalFiltered.Count >= 3)
        {
            variants.Add(string.Join(' ', seasonalFiltered));
        }

        if (lowered.Count >= 4 && lowered[0] == "budgeting" && lowered[1] == "for")
        {
            var audience = string.Join(' ', tokens.Skip(2).Where(t => !string.Equals(t, "seasonal", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(t, "^20\\d\\d$", RegexOptions.IgnoreCase)));
            if (!string.IsNullOrWhiteSpace(audience))
            {
                variants.Add($"budgeting for {audience}");
                variants.Add($"seasonal budgeting for {audience}");
                variants.Add($"budget planning for {audience}");
            }
        }

        variants.Add(primaryKeyword.Replace(" seasonal", string.Empty, StringComparison.OrdinalIgnoreCase).Trim());

        return variants
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Regex.Replace(v!, "\\s+", " ").Trim())
            .Where(v => !v.Equals(primaryKeyword, StringComparison.OrdinalIgnoreCase))
            .Where(IsApprovedVariant)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ClampVariantIndex(int index, int count)
        => count <= 0 ? 0 : Math.Max(0, Math.Min(index, count - 1));

    private static string BuildConservativeKeywordFallback(string primaryKeyword)
    {
        var tokens = primaryKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
        {
            return string.Join(' ', tokens.Take(Math.Max(2, tokens.Length - 1)));
        }

        return primaryKeyword;
    }

    private static string ReplaceExactPhraseOnce(string text, string exactPhrase, string replacement)
        => Regex.Replace(text, Regex.Escape(exactPhrase), replacement, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static string ReplaceFullPhraseVariant(string text, string exactPhrase, string replacement)
        => Regex.Replace(text, Regex.Escape(exactPhrase), replacement, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static bool IsApprovedVariant(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("for ", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith(" seasonal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !trimmed.Contains("this seasonal", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("scenario", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("strategy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadableHeading(string heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return false;
        }

        var trimmed = heading.Trim();
        return !trimmed.StartsWith("for ", StringComparison.OrdinalIgnoreCase)
            && !trimmed.EndsWith(" seasonal", StringComparison.OrdinalIgnoreCase)
            && !Regex.IsMatch(trimmed, "\\bfor\\s+[a-z]+\\s+seasonal$", RegexOptions.IgnoreCase);
    }

    private static string BuildSafeHeadingFallback(string originalHeading, string replacement)
    {
        var cleaned = Regex.Replace(originalHeading, "for .*", replacement, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return CleanHeading(cleaned.Contains(replacement, StringComparison.OrdinalIgnoreCase) ? cleaned : $"{CleanHeading(originalHeading)} ({replacement})");
    }

    private static string RemoveKeywordPhraseOnly(string text, string exactPhrase)
    {
        var updated = Regex.Replace(text, Regex.Escape(exactPhrase), string.Empty, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return Regex.Replace(updated, "\\s+", " ").Trim(' ', '-', ':', ';', ',');
    }

    private static string CleanHeading(string heading)
    {
        heading = Regex.Replace(heading, "\\s+", " ").Trim(' ', '-', ':', ';', ',');
        return string.IsNullOrWhiteSpace(heading) ? "Practical next step" : heading;
    }

    private sealed record SeoPhrasingValidationResult(bool IsValid, IReadOnlyList<string> Failures, IReadOnlyList<int> HeadingFailureIndexes)
    {
        public static SeoPhrasingValidationResult Valid() => new(true, Array.Empty<string>(), Array.Empty<int>());
    }

    private void LogArticleCheckpoint(ContentWorkflowTask task, ContentIdea? idea, string checkpoint, GeneratedLongformArticle article, SeoPhrasingValidationResult? validation, string? artifactSource)
    {
        var primaryKeyword = idea?.PrimaryKeyword?.Trim() ?? string.Empty;
        var headingExactCount = string.IsNullOrWhiteSpace(primaryKeyword)
            ? 0
            : article.Sections.Count(section => section.Heading.Contains(primaryKeyword, StringComparison.OrdinalIgnoreCase));
        var bodyExactCount = string.IsNullOrWhiteSpace(primaryKeyword)
            ? 0
            : Regex.Matches(article.BodyText ?? string.Empty, Regex.Escape(primaryKeyword), RegexOptions.IgnoreCase).Count;
        _logger.LogInformation(
            "Article checkpoint {Checkpoint}: an estimated {EstimatedWordCount} words across {SectionCount} sections. Quality checks are {ValidationOutcome}; this is not factual verification. WorkflowRunId={WorkflowRunId} TaskId={TaskId} HeadingExactCount={HeadingExactCount} BodyExactCount={BodyExactCount} IsSynthetic={IsSynthetic} ValidationCodes={ValidationCodes}",
            WorkflowDiagnostics.Identifier(checkpoint), article.EstimatedWordCount, article.Sections.Count,
            validation is null ? "not evaluated at this checkpoint" : validation.IsValid ? "passed" : "failed; review the named rules",
            task.ContentWorkflowJobId, task.Id, headingExactCount, bodyExactCount, article.IsSynthetic,
            validation is null ? "not-evaluated" : WorkflowDiagnostics.ValidationCodes(string.Join("; ", validation.Failures)));
    }

    /// <summary>Retains failure identity and stack methods without copying content-bearing exception messages into logs.</summary>
    /// <param name="exception">The actual exception.</param>
    /// <param name="explanation">A fixed explanation identifying the operation and next action.</param>
    /// <param name="runId">Existing workflow or task correlation identifier.</param>
    private void LogSafeFailure(Exception exception, string explanation, Guid runId)
    {
        var failure = WorkflowDiagnostics.Failure(exception);
        _logger.LogWarning("{Explanation} {NextAction} WorkflowRunId={WorkflowRunId} FailureCode={FailureCode} ExceptionType={ExceptionType} ExceptionCode={ExceptionCode} StackMethods={StackMethods}", explanation, failure.Summary, runId, failure.Code, failure.ExceptionType, failure.ExceptionCode, failure.StackMethods);
    }

    /// <summary>Builds a diagnostic template when no model draft is available.</summary>
    /// <remarks>This is synthetic scaffolding, never publishable writer output. Preserve that provenance through quality gates.</remarks>
    /// <param name="idea">The approved topic, if available.</param>
    /// <param name="secondaryKeywords">Supporting phrases for the template.</param>
    /// <param name="sourceSummaries">Available research summaries.</param>
    /// <returns>A synthetic article that does not meet the writer quality gate.</returns>
    private static GeneratedLongformArticle BuildLongformArticle(
        ContentIdea? idea,
        List<string> secondaryKeywords,
        List<string> sourceSummaries)
    {
        var title = idea?.Title ?? "Approved topic";
        var slug = idea?.SlugSuggestion ?? string.Empty;
        var keyword = Fallback(idea?.PrimaryKeyword, title);
        var summary = Fallback(idea?.Summary, $"A practical longform article about {keyword}.");
        var painPoint = Fallback(idea?.AudiencePainPoint, "feeling stuck, behind, or overwhelmed");
        var goal = Fallback(idea?.AudienceGoal, "make steady progress with a simple plan");
        var angle = Fallback(idea?.RecommendedAngle, $"Take a practical, low-friction approach to {keyword}");
        var whyNow = Fallback(idea?.WhyNow, $"Readers are actively looking for simpler, more realistic ways to handle {keyword} right now.");
        var contentType = NormalizeContentType(idea?.ContentType);
        var targetRange = GetWordTargetRange(contentType);
        var supportingKeywords = secondaryKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
        var evidence = sourceSummaries.Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToList();
        var headings = BuildSectionHeadings(contentType, keyword, goal);

        var introParagraphs = new List<string>
        {
            $"{title} starts with one practical goal: help the reader take a clear next step on {keyword} without getting buried in jargon or filler.",
            $"If the problem feels heavy or confusing, the fastest win is to break it into a few simple actions, use real numbers when they matter, and focus on what helps this week instead of building a perfect system.",
            $"That is why this article stays concrete. It turns {goal.TrimEnd('.')} into a sequence the reader can actually follow."
        };

        var sections = headings
            .Select((heading, index) => BuildSection(index, heading, keyword, painPoint, goal, angle, whyNow, supportingKeywords, evidence, contentType))
            .ToList();

        var conclusionParagraphs = new List<string>
        {
            $"The real value in {keyword} is not perfect execution on day one. It is getting clear enough to make one smart decision, take one useful step, and build from there.",
            $"Start small, use the evidence and examples that fit your situation, and review the result after the first attempt so the next step is easier than the first."
        };

        var callToAction = $"Pick one action from this guide and do it today so {goal.TrimEnd('.').ToLowerInvariant()} stops being theoretical and starts becoming real progress.";
        var metaDescription = $"{title} with practical steps, examples, and clear actions readers can use right away.";

        foreach (var section in sections)
        {
            if (section.Heading.Contains("step-by-step", StringComparison.OrdinalIgnoreCase))
            {
                section.Paragraphs.Add("- Start with the smallest useful version of the habit.\n- Repeat it on the same day each week.\n- Track the result before changing the system.");
            }

            if (section.Heading.Contains("tips", StringComparison.OrdinalIgnoreCase) || section.Heading.Contains("tool", StringComparison.OrdinalIgnoreCase) || section.Heading.Contains("faq", StringComparison.OrdinalIgnoreCase))
            {
                section.Paragraphs.Add("- Keep the setup simple.\n- Use one tool at a time.\n- Review what is working before adding complexity.");
            }
        }

        var bodyText = string.Join("\n\n", sections.Select(section => $"## {section.Heading}\n\n{section.BodyText}"));
        var fullText = string.Join("\n\n", new[]
        {
            $"# {title}",
            summary,
            string.Join("\n\n", introParagraphs),
            bodyText,
            "## Conclusion",
            string.Join("\n\n", conclusionParagraphs),
            "## Next step",
            callToAction
        });

        var estimatedWordCount = CountWords(fullText);
        var estimatedReadTimeMinutes = Math.Max(12, (int)Math.Ceiling(estimatedWordCount / 220m));

        return new GeneratedLongformArticle(
            title,
            slug,
            summary,
            metaDescription,
            targetRange.Min,
            targetRange.Max,
            estimatedWordCount,
            estimatedReadTimeMinutes,
            introParagraphs,
            sections,
            conclusionParagraphs,
            callToAction,
            bodyText,
            fullText,
            true,
            false);
    }

    private static bool CanTaskSucceed(ContentWorkflowTask task, GeneratedLongformArticle article)
    {
        return task.Name switch
        {
            "Research Topic and Intent" => !string.IsNullOrWhiteSpace(article.Title),
            "Build Keyword Strategy" => !string.IsNullOrWhiteSpace(article.Title),
            "Optimize SEO Package" => !string.IsNullOrWhiteSpace(article.Title),
            "Create Structured Outline" => article.Sections.Count >= 8,
            "Write Rule-Compliant Draft" => !article.IsSynthetic && article.MeetsMinimumQuality,
            "Apply SEO and Linking" => !article.IsSynthetic && article.EstimatedWordCount >= 1200,
            "Add Monetization and CTA" => !string.IsNullOrWhiteSpace(article.CallToAction),
            "Run Strict QA Scoring" => !article.IsSynthetic,
            "Humanize Final Draft" => !article.IsSynthetic,
            _ => true
        };
    }

    private static string BuildTaskFailureReason(ContentWorkflowTask task, GeneratedLongformArticle article)
    {
        if (task.Name is "Write Rule-Compliant Draft" or "Draft" && article.IsSynthetic)
        {
            return "Draft generation fell back to synthetic content. Blocking workflow until a real writer output succeeds.";
        }

        if (task.Name == "Create Structured Outline" && article.Sections.Count < 8)
        {
            return "Structured outline is missing required sections from the article template.";
        }

        if (task.Name == "Humanize Final Draft" && !article.MeetsMinimumQuality)
        {
            return "Humanize Final Draft received an article that still needs QA-driven revision, but the step should no longer be hard-blocked here.";
        }

        if (!article.MeetsMinimumQuality)
        {
            return $"Task '{task.Name}' blocked because the article did not meet the quality gate. If QA score is below 8, rerun the writer before continuing.";
        }

        return $"Task '{task.Name}' could not produce a valid output.";
    }

    private string BuildArticleHtmlBody(GeneratedLongformArticle article, IReadOnlyList<ArticleImageAsset> imageAssets)
    {
        var blocks = new List<string>();
        blocks.AddRange(article.IntroParagraphs.Select(RenderParagraphLikeBlock));

        if (article.Sections.Count > 0)
        {
            blocks.Add("<section class=\"article-read-section article-read-toc\"><h2>Table of Contents</h2><ul>" +
                       string.Join(string.Empty, article.Sections.Select(section => $"<li><a href=\"#{BuildAnchorId(section.Heading)}\">{System.Net.WebUtility.HtmlEncode(section.Heading)}</a></li>")) +
                       "<li><a href=\"#conclusion\">Conclusion</a></li><li><a href=\"#next-step\">Next step</a></li></ul></section>");
        }

        if (imageAssets.Count > 0)
        {
            blocks.Add(RenderImageBlock(imageAssets[0]));
        }

        var supportingImageIndex = 1;
        foreach (var section in article.Sections)
        {
            blocks.Add($"<section class=\"article-read-section\" id=\"{BuildAnchorId(section.Heading)}\"><h2>{System.Net.WebUtility.HtmlEncode(section.Heading)}</h2>{string.Join(string.Empty, section.Paragraphs.Select(RenderParagraphLikeBlock))}</section>");

            if (supportingImageIndex < imageAssets.Count)
            {
                blocks.Add(RenderImageBlock(imageAssets[supportingImageIndex]));
                supportingImageIndex++;
            }
        }

        if (article.ConclusionParagraphs.Any())
        {
            blocks.Add($"<section class=\"article-read-section\" id=\"conclusion\"><h2>Conclusion</h2>{string.Join(string.Empty, article.ConclusionParagraphs.Select(RenderParagraphLikeBlock))}</section>");
        }

        if (!string.IsNullOrWhiteSpace(article.CallToAction))
        {
            blocks.Add($"<section class=\"article-read-section\" id=\"next-step\"><h2>Next step</h2>{RenderParagraphLikeBlock(article.CallToAction)}</section>");
        }

        return string.Join("\n", blocks);
    }

    private async Task<GeneratedLongformArticle> EnhanceArticlePresentationAsync(Guid jobId, GeneratedLongformArticle article, IReadOnlyCollection<string> sourceSummaries, CancellationToken ct)
    {
        var linkedArticle = await ApplyInternalLinksAsync(sourceSummaries, article, ct);
        var imageAssets = await LoadArticleImageAssetsAsync(jobId, ct);
        if (imageAssets.Count == 0)
        {
            return linkedArticle;
        }

        var htmlBody = BuildArticleHtmlBody(linkedArticle, imageAssets);
        var plainBody = StripHtml(htmlBody);
        return linkedArticle with
        {
            BodyText = plainBody,
            FullText = BuildArticleFullText(linkedArticle, plainBody),
        };
    }

    private async Task<GeneratedLongformArticle> ApplyInternalLinksAsync(IReadOnlyCollection<string> sourceSummaries, GeneratedLongformArticle article, CancellationToken ct)
    {
        InternalLinkPlanResult? plan = null;
        try
        {
            plan = await _seoAndMonetizationAgent.AddInternalLinkingAsync(sourceSummaries, ct);
        }
        catch (Exception ex)
        {
            LogSafeFailure(ex, "The internal link plan could not be built. Review local article links before publishing.", Guid.Empty);
        }

        if (plan is null || plan.InternalLinkTargets.Length == 0)
        {
            return article;
        }

        var targets = plan.InternalLinkTargets
            .Select(ParseInternalLinkTarget)
            .Where(x => x is not null)
            .Cast<InternalLinkTarget>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Anchor) && !string.IsNullOrWhiteSpace(x.Url))
            .ToList();

        if (targets.Count == 0)
        {
            return article;
        }

        foreach (var target in targets)
        {
            // The 'Url' currently holds our heuristic slug (e.g. /articles/budgeting-for-beginners)
            var slug = target.Url.Replace("/articles/", "").Trim('/');

            // SQLite cannot translate ToLowerInvariant here, so filter in memory after narrowing the set.
            var candidateArticles = await _dbContext.Articles
                .Where(a => a.Title != null)
                .ToListAsync(ct);

            var existingArticle = candidateArticles.FirstOrDefault(a =>
                Regex.Replace(a.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') == slug);

            if (existingArticle != null)
            {
                // Use a consistent slugging for the final URL
                var finalSlug = Regex.Replace(existingArticle.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                
                // Replace the target in the list because InternalLinkTarget is a record (immutable)
                int idx = targets.IndexOf(target);
                if (idx != -1)
                {
                    targets[idx] = target with { Url = $"/articles/{finalSlug}" };
                }
            }
        }

        var updatedIntro = article.IntroParagraphs.ToList();
        var updatedSections = article.Sections.Select(section => new GeneratedSection(section.Heading, section.Paragraphs.ToList())).ToList();
        var updatedConclusion = article.ConclusionParagraphs.ToList();
        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            if (TryInsertInternalLink(updatedIntro, target, usedTargets))
            {
                continue;
            }

            var inserted = false;
            for (var i = 0; i < updatedSections.Count; i++)
            {
                var section = updatedSections[i];
                var paragraphs = section.Paragraphs.ToList();
                if (TryInsertInternalLink(paragraphs, target, usedTargets))
                {
                    updatedSections[i] = new GeneratedSection(section.Heading, paragraphs);
                    inserted = true;
                    break;
                }
            }

            if (!inserted)
            {
                TryInsertInternalLink(updatedConclusion, target, usedTargets);
            }
        }

        var bodyText = string.Join("\n\n", updatedSections.Select(section => $"## {section.Heading}\n\n{section.BodyText}"));
        var fullText = string.Join("\n\n", new[]
        {
            $"# {article.Title}",
            article.Summary,
            string.Join("\n\n", updatedIntro),
            bodyText,
            "## Conclusion",
            string.Join("\n\n", updatedConclusion),
            "## Next step",
            article.CallToAction
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return article with
        {
            IntroParagraphs = updatedIntro,
            Sections = updatedSections,
            ConclusionParagraphs = updatedConclusion,
            BodyText = bodyText,
            FullText = fullText
        };
    }

    private async Task<List<ArticleImageAsset>> LoadArticleImageAssetsAsync(Guid jobId, CancellationToken ct)
    {
        var artifacts = await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == jobId && (x.ArtifactType == "FeaturedImageAsset" || x.ArtifactType == "SupportingImageAsset"))
            .OrderBy(x => x.ArtifactType == "FeaturedImageAsset" ? 0 : 1)
            .ThenBy(x => x.CreatedUtc)
            .ToListAsync(ct);

        var assets = new List<ArticleImageAsset>();
        foreach (var artifact in artifacts)
        {
            try
            {
                using var doc = JsonDocument.Parse(artifact.ContentJson);
                var root = doc.RootElement;
                var imagePath = GetJsonString(root, "imagePath");
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    continue;
                }

                assets.Add(new ArticleImageAsset(
                    imagePath,
                    GetJsonString(root, "altText"),
                    GetJsonString(root, "title"),
                    GetJsonString(root, "suggestedPlacement"),
                    artifact.ArtifactType == "FeaturedImageAsset"));
            }
            catch (Exception ex)
            {
                LogSafeFailure(ex, "A saved image artifact could not be read. Review image artifacts for this workflow.", jobId);
            }
        }

        return assets;
    }

    private static InternalLinkTarget? ParseInternalLinkTarget(object target)
    {
        if (target is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(target);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var anchor = GetJsonString(root, "anchor");
            var targetText = GetJsonString(root, "target");
            return string.IsNullOrWhiteSpace(anchor) || string.IsNullOrWhiteSpace(targetText)
                ? null
                : new InternalLinkTarget(anchor, BuildInternalArticleUrl(targetText));
        }
        catch
        {
            return null;
        }
    }

    private static string BuildInternalArticleUrl(string target)
    {
        var slug = Regex.Replace((target ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "/articles" : $"/articles/{slug}";
    }

    private static bool TryInsertInternalLink(List<string> paragraphs, InternalLinkTarget target, HashSet<string> usedTargets)
    {
        if (usedTargets.Contains(target.Url))
        {
            return false;
        }

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            if (string.IsNullOrWhiteSpace(paragraph) || paragraph.Contains(target.Url, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pattern = $"\\b{Regex.Escape(target.Anchor)}\\b";
            if (!Regex.IsMatch(paragraph, pattern, RegexOptions.IgnoreCase))
            {
                continue;
            }

            paragraphs[i] = Regex.Replace(
                paragraph,
                pattern,
                $"[$0]({target.Url})",
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));
            usedTargets.Add(target.Url);
            return true;
        }

        return false;
    }

    private static string RenderImageBlock(ArticleImageAsset asset)
    {
        var caption = string.IsNullOrWhiteSpace(asset.Title) ? asset.AltText : asset.Title;
        return $"<figure class=\"article-inline-image\"><img src=\"{EscapeHtmlAttribute(asset.ImagePath)}\" alt=\"{EscapeHtmlAttribute(asset.AltText)}\" /><figcaption>{System.Net.WebUtility.HtmlEncode(caption)}</figcaption></figure>";
    }

    private static string BuildArticleFullText(GeneratedLongformArticle article, string plainBody)
    {
        return string.Join("\n\n", new[]
        {
            $"# {article.Title}",
            article.Summary,
            plainBody,
            "## Next step",
            article.CallToAction
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string StripHtml(string html)
    {
        var plain = Regex.Replace(html ?? string.Empty, "<[^>]+>", " ");
        plain = System.Net.WebUtility.HtmlDecode(plain);
        return Regex.Replace(plain, @"\s+", " ").Trim();
    }

    private static string EscapeHtmlAttribute(string value)
        => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        }

        return string.Empty;
    }

    private static string RenderParagraphLikeBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0 && lines.All(line => line.StartsWith("- ", StringComparison.Ordinal)))
        {
            return "<ul>" + string.Join(string.Empty, lines.Select(line => $"<li>{FormatInlineMarkup(line[2..])}</li>")) + "</ul>";
        }

        if (lines.Length > 0 && lines.All(line => Regex.IsMatch(line, "^\\d+\\.\\s")))
        {
            return "<ol>" + string.Join(string.Empty, lines.Select(line => $"<li>{FormatInlineMarkup(Regex.Replace(line, "^\\d+\\.\\s", string.Empty))}</li>")) + "</ol>";
        }

        return $"<p>{FormatInlineMarkup(text)}</p>";
    }

    private static string FormatInlineMarkup(string text)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(text);
        return Regex.Replace(encoded, "\\*\\*(.+?)\\*\\*", "<strong>$1</strong>");
    }

    private static string BuildAnchorId(string heading)
        => Regex.Replace((heading ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static KeywordStrength BuildKeywordStrength(ContentIdea? idea)
    {
        if (idea is null)
        {
            return new KeywordStrength("Unknown", 0m, "No keyword scoring data available yet.");
        }

        var strengthScore = Math.Round(Math.Clamp(
            (idea.SeoOpportunityScore * 0.55m) +
            (idea.OverallScore * 0.30m) +
            ((100m - idea.CompetitionScore) * 0.15m),
            0m,
            100m), 1);

        var label = strengthScore switch
        {
            >= 80m => "Very Strong",
            >= 65m => "Strong",
            >= 50m => "Moderate",
            >= 35m => "Weak",
            _ => "Very Weak"
        };

        return new KeywordStrength(
            label,
            strengthScore,
            $"SEO {idea.SeoOpportunityScore:0.#}, overall {idea.OverallScore:0.#}, competition {idea.CompetitionScore:0.#}.");
    }

    private static GeneratedSection BuildSection(
        int index,
        string heading,
        string keyword,
        string painPoint,
        string goal,
        string angle,
        string whyNow,
        List<string> secondaryKeywords,
        List<string> evidence,
        string contentType)
    {
        var supportingKeyword = secondaryKeywords.ElementAtOrDefault(index % Math.Max(1, secondaryKeywords.Count)) ?? keyword;
        var evidenceNote = evidence.ElementAtOrDefault(index % Math.Max(1, evidence.Count));

        if (heading.Contains("What You'll Learn", StringComparison.OrdinalIgnoreCase) || heading.Contains("What You’ll Learn", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"- What {keyword} means in plain English.\n- Which steps matter first if the reader wants to {goal.TrimEnd('.').ToLowerInvariant()}.\n- How to avoid the most common mistakes without overcomplicating the process."
            });
        }

        if (heading.Contains("Why most people struggle", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"Most people get stuck on {keyword} because the advice they find is either too vague, too complex, or disconnected from the real pressure they are under. When someone is dealing with {painPoint.TrimEnd('.')}, generic tips are easy to ignore.",
                $"A better approach is to reduce the problem to one or two decisions at a time, use plain language, and show what success looks like in real life instead of describing an ideal system.",
                ComposeEvidenceParagraph(evidenceNote, keyword)
            });
        }

        if (heading.Contains("baseline numbers", StringComparison.OrdinalIgnoreCase) || heading.Contains("real-world context", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"This is where concrete numbers help. Even a simple example, rough timeline, or decision threshold makes {keyword} easier to follow than broad advice alone.",
                $"For example, if the reader is trying to {goal.TrimEnd('.').ToLowerInvariant()}, show what the first target looks like, what tradeoffs might appear, and what a realistic starting point could be this week.",
                $"The point is not perfect math. The point is helping the reader understand what is enough to get moving."
            });
        }

        if (heading.Contains("simple framework", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"A simple framework works best here: identify the immediate goal, choose the smallest useful action, and review the result before adding complexity.",
                $"That keeps {keyword} grounded in real decisions instead of turning it into a pile of disconnected advice.",
                $"If a tool, checklist, or comparison makes the next move clearer, use it. If it adds friction, strip it out."
            });
        }

        if (heading.Contains("step-by-step", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"Step 1: define the immediate outcome. The reader should know exactly what they are trying to accomplish with {keyword} in the next few days, not someday.",
                $"Step 2: choose one concrete action and put a number, date, or decision rule on it. That turns advice into something trackable.",
                $"Step 3: review the first result, keep what worked, and adjust the next step without rebuilding the whole plan."
            });
        }

        if (heading.Contains("tips", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"Keep the setup lighter than you think it needs to be. Readers are more likely to follow through on {keyword} when the first step is obvious and fast.",
                $"Use one tool or reference at a time so the process stays understandable.",
                $"When possible, turn advice into a checklist, short review habit, or simple comparison the reader can repeat."
            });
        }

        if (heading.Contains("Tools", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"The best tools for {keyword} are the ones that reduce friction, not the ones with the most features.",
                $"A worksheet, checklist, calculator, or trusted reference page can all be useful if they make the next decision easier.",
                $"If a tool adds confusion, skip it and return to the simplest version of the process."
            });
        }

        if (heading.Contains("Common mistakes", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"One common mistake is trying to solve every edge case before taking the first step. That slows progress and makes {keyword} feel harder than it needs to be.",
                $"Another is copying advice that sounds smart but does not fit the reader's real constraints, timeline, or energy level.",
                $"A better standard is simple, repeatable progress that can survive a messy week."
            });
        }

        if (heading.Contains("FAQ", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"What is the fastest way to start with {keyword}? Start with the smallest decision that removes uncertainty and creates a clear next action.",
                $"How detailed does the plan need to be? Detailed enough to act on, but not so detailed that it becomes hard to maintain.",
                $"What if the first try does not work? Adjust the next step, not the entire strategy."
            });
        }

        if (heading.Contains("next step", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedSection(heading, new List<string>
            {
                $"Choose one action from this article that directly supports {goal.TrimEnd('.').ToLowerInvariant()}.",
                $"Do it today or schedule it before the day ends.",
                $"Then review what changed so the second step is easier than the first."
            });
        }

        var paragraphs = new[]
        {
            $"Start with the clearest version of {keyword} you can explain in one sentence.",
            $"Use {supportingKeyword} only if it helps the reader act faster, understand a tradeoff, or make a clearer decision.",
            ComposeEvidenceParagraph(evidenceNote, keyword)
        };

        return new GeneratedSection(heading, paragraphs.ToList());
    }

    private static (int Min, int Max) GetWordTargetRange(string contentType)
        => contentType switch
        {
            "PillarArticle" => (3500, 5000),
            "BestOfArticle" => (3500, 5000),
            "ComparisonArticle" => (3200, 4500),
            "ReviewArticle" => (3000, 4200),
            "AlternativeArticle" => (3000, 4200),
            "CaseStudyArticle" => (2800, 4200),
            "HowToArticle" => (2500, 3500),
            "MistakesArticle" => (2500, 3500),
            "FaqArticle" => (2500, 3200),
            "ChecklistArticle" => (2500, 3200),
            _ => (2500, 4000)
        };

    private static List<string> BuildSectionHeadings(string contentType, string keyword, string goal)
    {
        var common = new List<string>
        {
            "What You'll Learn",
            "Why most people struggle with this",
            "The baseline numbers and real-world context",
            $"A simple framework for {keyword}",
            $"A step-by-step plan to make {keyword} work",
            "Practical tips and strategies for real life",
            "Tools that make this easier",
            "Common mistakes to avoid",
            $"FAQ about {keyword}",
            $"Your next step toward {goal}"
        };

        return contentType switch
        {
            "ComparisonArticle" =>
            [
                "Why this comparison matters",
                "What to compare first",
                "Where each option wins",
                "Where each option falls short",
                "How to choose based on real constraints",
                "Mistakes buyers make during comparison",
                "The best next step after the comparison"
            ],
            "BestOfArticle" =>
            [
                "What makes an option worth recommending",
                "How to narrow the field fast",
                "Who each type of option is best for",
                "Where the biggest differences show up",
                "How to avoid choosing on hype alone",
                "Mistakes people make when picking the best option",
                "A practical way to make your decision"
            ],
            "FaqArticle" =>
            [
                "Why people keep asking this question",
                "The short answer",
                "The fuller explanation",
                "How this applies in real life",
                "What people often misunderstand",
                "Questions that should be answered next",
                "The one next step that matters most"
            ],
            _ => common
        };
    }

    private static string ComposeEvidenceParagraph(string? evidenceNote, string keyword)
    {
        if (string.IsNullOrWhiteSpace(evidenceNote))
        {
            return $"A useful longform article also benefits from grounded evidence. Even when the final piece is editorial rather than academic, it should reflect the language people actually use around {keyword}, the recurring blockers they describe, and the patterns that show up repeatedly in real search and discussion behavior.";
        }

        return $"One reason this topic holds up in longform is that the supporting evidence points in the same direction. In the research layer, a recurring signal was: {evidenceNote.TrimEnd('.')}. That kind of evidence helps the article stay tied to real demand instead of drifting into generic filler or thin keyword-chasing copy.";
    }

    private static int CountWords(string value)
        => string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? "LongFormBlogArticle" : contentType.Trim();

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private sealed record KeywordStrength(string Label, decimal Score, string Reason);

    private static string FormatChecklistSummary(SeoValidationChecklist c)
        => $"SEO Checklist: Title={Tick(c.KeywordInTitle)} Intro={Tick(c.KeywordInIntro)} Headings={Tick(c.KeywordInHeadings)} FAQ={Tick(c.FaqSectionExists)}({c.FaqCount}) Links={c.InternalLinksCount} Coverage={c.KeywordCoverageScore}/10 Flow={Tick(c.ArticleFlowsNaturally)}{(c.Failures.Length > 0 ? $" FAIL: {string.Join("; ", c.Failures)}" : " ✓")}";

    private static string Tick(bool v) => v ? "✓" : "✗";

    private static string BuildArtifactText(string artifactType, object payload)
        => $"{artifactType}\n{JsonSerializer.Serialize(payload, JsonOptions)}";

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private sealed record InternalLinkTarget(string Anchor, string Url);

    private sealed record ArticleImageAsset(string ImagePath, string AltText, string Title, string SuggestedPlacement, bool IsFeatured);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonOptionsWithCaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
