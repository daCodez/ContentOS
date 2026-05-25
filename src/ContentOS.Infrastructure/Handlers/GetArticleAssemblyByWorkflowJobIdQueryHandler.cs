using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Handlers;

public class GetArticleAssemblyByWorkflowJobIdQueryHandler
    : IRequestHandler<GetArticleAssemblyByWorkflowJobIdQuery, ArticleAssemblyDto>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<GetArticleAssemblyByWorkflowJobIdQueryHandler> _logger;

    public GetArticleAssemblyByWorkflowJobIdQueryHandler(
        ContentOsDbContext dbContext,
        ILogger<GetArticleAssemblyByWorkflowJobIdQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ArticleAssemblyDto> Handle(
        GetArticleAssemblyByWorkflowJobIdQuery request,
        CancellationToken cancellationToken)
    {
        var workflowJobId = request.WorkflowJobId;

        var workflowJob = await _dbContext.ContentWorkflowJobs
            .FirstOrDefaultAsync(j => j.Id == workflowJobId, cancellationToken);

        if (workflowJob == null)
        {
            _logger.LogWarning("Workflow job {WorkflowJobId} not found.", workflowJobId);
            return null!;
        }

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(t => t.ContentWorkflowJobId == workflowJobId)
            .Select(t => new
            {
                t.Name,
                t.AssignedAgent,
                t.OutputDataJson,
                t.DisplayOrder,
                t.Status
            })
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (!tasks.Any())
        {
            _logger.LogWarning("No tasks found for workflow job {WorkflowJobId}.", workflowJobId);
            return null!;
        }

        var writeDraftTask = tasks.FirstOrDefault(t => t.Name == WorkflowTaskNames.WriteDraft);
        var humanizeTask = tasks.FirstOrDefault(t => t.Name == WorkflowTaskNames.Humanize);
        var qaTask = tasks.FirstOrDefault(t => t.Name == WorkflowTaskNames.Qa);

        if (writeDraftTask == null)
        {
            _logger.LogError("Required draft task missing for workflow {WorkflowJobId}.", workflowJobId);
            return null!;
        }

        var writeDraftOutput = ParseTaskOutput<WriteDraftOutput>(writeDraftTask.OutputDataJson);
        var humanizeOutput = humanizeTask != null
            ? ParseTaskOutput<HumanizeOutput>(humanizeTask.OutputDataJson)
            : null;
        var qaOutput = qaTask != null
            ? ParseTaskOutput<QaOutput>(qaTask.OutputDataJson)
            : null;

        var finalPreviewArtifact = await _dbContext.ContentArtifacts
            .Where(a => a.ContentWorkflowJobId == workflowJobId && a.ArtifactType == "FinalArticlePreview")
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var finalPreviewOutput = finalPreviewArtifact is not null
            ? ParseTaskOutput<FinalArticlePreviewOutput>(finalPreviewArtifact.ContentJson)
            : null;

        if (writeDraftOutput == null && finalPreviewOutput == null)
        {
            _logger.LogError("Failed to parse article content for workflow {WorkflowJobId}.", workflowJobId);
            return null!;
        }

        var resolvedTitle = finalPreviewOutput?.Title
            ?? humanizeOutput?.Title
            ?? writeDraftOutput?.Title
            ?? finalPreviewArtifact?.Title
            ?? string.Empty;

        var resolvedSlug = finalPreviewOutput?.Slug
            ?? humanizeOutput?.Slug
            ?? writeDraftOutput?.Slug
            ?? string.Empty;

        var resolvedSummary = finalPreviewOutput?.Summary
            ?? humanizeOutput?.Summary
            ?? writeDraftOutput?.Summary
            ?? string.Empty;

        var resolvedMetaDescription = finalPreviewOutput?.MetaDescription
            ?? humanizeOutput?.MetaDescription
            ?? writeDraftOutput?.MetaDescription
            ?? string.Empty;

        var resolvedContentType = finalPreviewOutput?.ContentType
            ?? humanizeOutput?.ContentType
            ?? writeDraftOutput?.ContentType
            ?? string.Empty;

        return new ArticleAssemblyDto
        {
            WorkflowJobId = workflowJobId,
            ContentIdeaId = workflowJob.ContentIdeaId,
            WorkflowStatus = workflowJob.Status ?? "Unknown",
            Title = resolvedTitle,
            Slug = resolvedSlug,
            Summary = resolvedSummary,
            MetaDescription = resolvedMetaDescription,
            ContentType = resolvedContentType,
            QaScore = qaOutput?.OverallScore,
            QaPassed = string.Equals(qaOutput?.QaStatus, "Pass", StringComparison.OrdinalIgnoreCase),
            QaFeedback = qaOutput?.Recommendations ?? new List<string>(),
            QaHardRuleFailures = qaOutput?.HardRuleFailures ?? new List<string>(),
            EstimatedWordCount = finalPreviewOutput?.EstimatedWordCount ?? writeDraftOutput?.EstimatedWordCount ?? 0,
            EstimatedReadTimeMinutes = finalPreviewOutput?.EstimatedReadTimeMinutes ?? writeDraftOutput?.EstimatedReadTimeMinutes ?? 0,
            PublishReady = finalPreviewOutput?.PublishReady ?? humanizeOutput?.PublishReady ?? false,
            Content = new ArticleContentDto
            {
                Intro = finalPreviewOutput?.Intro ?? writeDraftOutput?.IntroParagraphs ?? new List<string>(),
                Sections = finalPreviewOutput?.Sections?.Select(s => new ArticleSectionDto
                {
                    Heading = s.Heading ?? string.Empty,
                    Paragraphs = s.Paragraphs ?? new List<string>()
                }).ToList()
                    ?? writeDraftOutput?.Sections?.Select(s => new ArticleSectionDto
                    {
                        Heading = s.Heading ?? string.Empty,
                        Paragraphs = s.Paragraphs ?? new List<string>()
                    }).ToList()
                    ?? new List<ArticleSectionDto>(),
                Conclusion = finalPreviewOutput?.Conclusion ?? writeDraftOutput?.ConclusionParagraphs ?? new List<string>(),
                Cta = finalPreviewOutput?.Cta ?? writeDraftOutput?.CallToAction ?? string.Empty
            }
        };
    }

    private T? ParseTaskOutput<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON as {Type}", typeof(T).Name);
            return null;
        }
    }

    private static class WorkflowTaskNames
    {
        public const string WriteDraft = "Write Rule-Compliant Draft";
        public const string Humanize = "Humanize Final Draft";
        public const string Qa = "Run Strict QA Scoring";
    }

    private class WriteDraftOutput
    {
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? MetaDescription { get; set; }
        public string? ContentType { get; set; }
        public int? TargetWordCountMin { get; set; }
        public int? TargetWordCountMax { get; set; }
        public List<string>? IntroParagraphs { get; set; }
        public List<WriteDraftSection>? Sections { get; set; }
        public List<string>? ConclusionParagraphs { get; set; }
        public string? CallToAction { get; set; }
        public int EstimatedWordCount { get; set; }
        public int EstimatedReadTimeMinutes { get; set; }
    }

    private class WriteDraftSection
    {
        public string? Heading { get; set; }
        public List<string>? Paragraphs { get; set; }
    }

    private class HumanizeOutput
    {
        public bool PublishReady { get; set; }
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? MetaDescription { get; set; }
        public string? ContentType { get; set; }
        public int EstimatedWordCount { get; set; }
    }

    private class QaOutput
    {
        public string? QaStatus { get; set; }
        public double OverallScore { get; set; }
        public List<string>? Recommendations { get; set; }
        public List<string>? HardRuleFailures { get; set; }
        public bool SyntheticFallbackUsed { get; set; }
        public bool RewriteRequired { get; set; }
    }

    private class FinalArticlePreviewOutput
    {
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? MetaDescription { get; set; }
        public string? ContentType { get; set; }
        public bool PublishReady { get; set; }
        public int EstimatedWordCount { get; set; }
        public int EstimatedReadTimeMinutes { get; set; }
        public List<string>? Intro { get; set; }
        public List<WriteDraftSection>? Sections { get; set; }
        public List<string>? Conclusion { get; set; }
        public string? Cta { get; set; }
    }
}
