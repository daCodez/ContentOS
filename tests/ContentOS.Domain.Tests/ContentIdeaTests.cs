using Xunit;
using ContentOS.Domain.Entities;
using FluentAssertions;

namespace ContentOS.Domain.Tests;

public class ContentIdeaTests
{
    [Fact]
    public void ContentIdea_DefaultValues_AreSetCorrectly()
    {
        var idea = new ContentIdea();

        idea.Id.Should().Be(Guid.Empty);
        idea.Status.Should().Be("Discovered");
        idea.ContentType.Should().Be("LongFormBlogArticle");
        idea.SecondaryKeywordsJson.Should().Be("[]");
        idea.SourceSummaryJson.Should().Be("[]");
        idea.TopicType.Should().Be("Problem");
        idea.MonetizationViable.Should().BeTrue();
        idea.CompetitionModifier.Should().Be(1m);
        idea.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        idea.UpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ContentIdea_CanTransitionToApproved()
    {
        var idea = new ContentIdea { Status = "Discovered" };

        idea.Status = "Approved";
        idea.ApprovedUtc = DateTime.UtcNow;
        idea.ApprovedBy = "Eric";

        idea.Status.Should().Be("Approved");
        idea.ApprovedUtc.Should().NotBeNull();
        idea.ApprovedBy.Should().Be("Eric");
    }

    [Fact]
    public void ContentIdea_CanBeArchived()
    {
        var idea = new ContentIdea { Status = "NeedsReview" };

        idea.Status = "Archived";

        idea.Status.Should().Be("Archived");
    }

    [Fact]
    public void ContentIdea_ScoresDefaultToZero()
    {
        var idea = new ContentIdea();

        idea.MonetizationFitScore.Should().Be(0m);
        idea.SeoOpportunityScore.Should().Be(0m);
        idea.TrendScore.Should().Be(0m);
        idea.CompetitionScore.Should().Be(0m);
        idea.OverallScore.Should().Be(0m);
        idea.IntentMatchScore.Should().Be(0m);
        idea.UniquenessScore.Should().Be(0m);
        idea.ClickPotentialScore.Should().Be(0m);
        idea.MonetizationPotentialScore.Should().Be(0m);
        idea.LowCompetitionBoost.Should().Be(0m);
    }

    [Fact]
    public void ContentIdea_NullableDatesDefaultToNull()
    {
        var idea = new ContentIdea();

        idea.ApprovedUtc.Should().BeNull();
        idea.RejectedUtc.Should().BeNull();
        idea.ApprovedBy.Should().BeNull();
        idea.MonetizationWarning.Should().BeNull();
    }
}