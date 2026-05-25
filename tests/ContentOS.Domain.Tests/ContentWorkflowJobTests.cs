using Xunit;
using ContentOS.Domain.Entities;
using FluentAssertions;

namespace ContentOS.Domain.Tests;

public class ContentWorkflowJobTests
{
    [Fact]
    public void ContentWorkflowJob_DefaultValues_AreSetCorrectly()
    {
        var job = new ContentWorkflowJob();

        job.Id.Should().Be(Guid.Empty);
        job.Status.Should().Be("Queued");
        job.CurrentStage.Should().BeEmpty();
        job.ErrorMessage.Should().BeEmpty();
        job.LastUpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        job.StartedUtc.Should().BeNull();
        job.CompletedUtc.Should().BeNull();
    }

    [Fact]
    public void ContentWorkflowJob_CanTransitionToInProgress()
    {
        var job = new ContentWorkflowJob { Status = "Queued" };

        job.Status = "InProgress";
        job.StartedUtc = DateTime.UtcNow;
        job.CurrentStage = "Research";

        job.Status.Should().Be("InProgress");
        job.StartedUtc.Should().NotBeNull();
        job.CurrentStage.Should().Be("Research");
    }

    [Fact]
    public void ContentWorkflowJob_CanTransitionToCompleted()
    {
        var job = new ContentWorkflowJob { Status = "InProgress" };

        job.Status = "Completed";
        job.CompletedUtc = DateTime.UtcNow;
        job.CurrentStage = "Completed";

        job.Status.Should().Be("Completed");
        job.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public void ContentWorkflowJob_CanTransitionToFailed()
    {
        var job = new ContentWorkflowJob { Status = "InProgress" };

        job.Status = "Failed";
        job.ErrorMessage = "Writer agent failed";

        job.Status.Should().Be("Failed");
        job.ErrorMessage.Should().Be("Writer agent failed");
    }
}