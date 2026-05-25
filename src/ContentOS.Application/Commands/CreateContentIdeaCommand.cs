using MediatR;

namespace ContentOS.Application.Commands;

public record CreateContentIdeaCommand(
    Guid SiteId,
    string Title,
    string PrimaryKeyword,
    string? Summary = null,
    string ContentType = "LongFormBlogArticle",
    string? SecondaryKeywordsJson = null,
    string? SearchIntent = null,
    string? AudiencePainPoint = null,
    string? AudienceGoal = null,
    string? RecommendedAngle = null,
    string? WhyNow = null,
    string? FunnelStage = null,
    decimal? MonetizationFitScore = null,
    decimal? SeoOpportunityScore = null,
    decimal? TrendScore = null,
    decimal? CompetitionScore = null,
    decimal? OverallScore = null,
    bool Evergreen = false,
    bool Seasonal = false,
    string? SourceSummaryJson = null,
    string Status = "NeedsReview") : IRequest<Guid>;
