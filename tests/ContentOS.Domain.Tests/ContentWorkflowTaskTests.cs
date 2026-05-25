using Xunit;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using FluentAssertions;

namespace ContentOS.Domain.Tests;

public class ContentWorkflowTaskTests
{
    [Fact]
    public void ContentWorkflowTask_DefaultValues_AreSetCorrectly()
    {
        var task = new ContentWorkflowTask();

        task.Id.Should().Be(Guid.Empty);
        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Pending);
        task.InputDataJson.Should().Be("{}");
        task.OutputDataJson.Should().Be("{}");
        task.ErrorMessage.Should().BeEmpty();
        task.RetryCount.Should().Be(0);
        task.LastUpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        task.StartedUtc.Should().BeNull();
        task.CompletedUtc.Should().BeNull();
    }

    [Fact]
    public void ContentWorkflowTask_CanTransitionToReady()
    {
        var task = new ContentWorkflowTask { Status = ContentOS.Domain.Enums.TaskStatus.Pending };

        task.Status = ContentOS.Domain.Enums.TaskStatus.Ready;

        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
    }

    [Fact]
    public void ContentWorkflowTask_CanTransitionToInProgress()
    {
        var task = new ContentWorkflowTask { Status = ContentOS.Domain.Enums.TaskStatus.Ready };

        task.Status = ContentOS.Domain.Enums.TaskStatus.InProgress;
        task.StartedUtc = DateTime.UtcNow;

        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.InProgress);
        task.StartedUtc.Should().NotBeNull();
    }

    [Fact]
    public void ContentWorkflowTask_CanTransitionToCompleted()
    {
        var task = new ContentWorkflowTask { Status = ContentOS.Domain.Enums.TaskStatus.InProgress };

        task.Status = ContentOS.Domain.Enums.TaskStatus.Completed;
        task.CompletedUtc = DateTime.UtcNow;
        task.OutputDataJson = "{\"result\": \"done\"}";

        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Completed);
        task.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public void ContentWorkflowTask_CanTransitionToFailed()
    {
        var task = new ContentWorkflowTask { Status = ContentOS.Domain.Enums.TaskStatus.InProgress };

        task.Status = ContentOS.Domain.Enums.TaskStatus.Failed;
        task.ErrorMessage = "Timeout";

        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Failed);
        task.ErrorMessage.Should().Be("Timeout");
    }

    [Fact]
    public void ContentWorkflowTask_RetryCountTracksRetries()
    {
        var task = new ContentWorkflowTask();

        task.RetryCount.Should().Be(0);

        task.RetryCount = 1;
        task.RetryCount.Should().Be(1);

        task.RetryCount = 2;
        task.RetryCount.Should().Be(2);
    }
}