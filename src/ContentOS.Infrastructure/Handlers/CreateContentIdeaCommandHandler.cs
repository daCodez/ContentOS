using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Handlers;

public class CreateContentIdeaCommandHandler : IRequestHandler<CreateContentIdeaCommand, Guid>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<CreateContentIdeaCommandHandler> _logger;

    public CreateContentIdeaCommandHandler(ContentOsDbContext dbContext, ILogger<CreateContentIdeaCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateContentIdeaCommand request, CancellationToken cancellationToken)
    {
        if (request.SiteId == Guid.Empty)
        {
            throw new ArgumentException("SiteId is required.", nameof(request.SiteId));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.", nameof(request.Title));
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryKeyword))
        {
            throw new ArgumentException("PrimaryKeyword is required.", nameof(request.PrimaryKeyword));
        }

        var siteExists = await _dbContext.Sites.AnyAsync(x => x.Id == request.SiteId && x.IsActive, cancellationToken);
        if (!siteExists)
        {
            throw new InvalidOperationException("Selected site does not exist or is inactive.");
        }

        var now = DateTime.UtcNow;
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            SiteId = request.SiteId,
            Title = request.Title.Trim(),
            PrimaryKeyword = request.PrimaryKeyword.Trim(),
            Summary = request.Summary?.Trim() ?? string.Empty,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "LongFormBlogArticle" : request.ContentType.Trim(),
            SecondaryKeywordsJson = NormalizeJsonArray(request.SecondaryKeywordsJson),
            SearchIntent = request.SearchIntent?.Trim() ?? string.Empty,
            AudiencePainPoint = request.AudiencePainPoint?.Trim() ?? string.Empty,
            AudienceGoal = request.AudienceGoal?.Trim() ?? string.Empty,
            RecommendedAngle = request.RecommendedAngle?.Trim() ?? string.Empty,
            WhyNow = request.WhyNow?.Trim() ?? string.Empty,
            FunnelStage = request.FunnelStage?.Trim() ?? string.Empty,
            MonetizationFitScore = request.MonetizationFitScore ?? 0,
            SeoOpportunityScore = request.SeoOpportunityScore ?? 0,
            TrendScore = request.TrendScore ?? 0,
            CompetitionScore = request.CompetitionScore ?? 0,
            OverallScore = request.OverallScore ?? 0,
            Evergreen = request.Evergreen,
            Seasonal = request.Seasonal,
            SourceSummaryJson = NormalizeJsonArray(request.SourceSummaryJson),
            SlugSuggestion = BuildSlug(request.Title),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "NeedsReview" : request.Status.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created content idea {ContentIdeaId} for site {SiteId}", idea.Id, idea.SiteId);

        return idea.Id;
    }

    private static string BuildSlug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string NormalizeJsonArray(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "[]" : value.Trim();
    }
}
