using Xunit;
using ContentOS.Domain.Entities;
using FluentAssertions;

namespace ContentOS.Domain.Tests;

public class WorkflowTemplateTests
{
    [Fact]
    public void WorkflowTemplate_DefaultValues_AreSetCorrectly()
    {
        var template = new WorkflowTemplate();

        template.Id.Should().Be(Guid.Empty);
        template.Name.Should().BeEmpty();
        template.Description.Should().BeEmpty();
        template.IsActive.Should().BeTrue();
        template.Version.Should().Be(1);
        template.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        template.LastUpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WorkflowTaskTemplate_DefaultValues_AreSetCorrectly()
    {
        var taskTemplate = new WorkflowTaskTemplate();

        taskTemplate.Id.Should().Be(Guid.Empty);
        taskTemplate.Name.Should().BeEmpty();
        taskTemplate.StageName.Should().BeEmpty();
        taskTemplate.AssignedAgent.Should().BeEmpty();
        taskTemplate.InstructionsTemplate.Should().BeEmpty();
        taskTemplate.RequiredInputsJson.Should().Be("[]");
        taskTemplate.ExpectedOutputsJson.Should().Be("[]");
        taskTemplate.AutoStartWhenPreviousComplete.Should().BeTrue();
        taskTemplate.IsApprovalRequired.Should().BeFalse();
        taskTemplate.IsActive.Should().BeTrue();
        taskTemplate.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void WorkflowTaskTemplate_CanRepresentAllCanonicalAgents()
    {
        var canonicalAgents = new[]
        {
            "content-research", "keyword-strategy", "seo-optimization-loop",
            "content-planner", "content-writer", "content-seo",
            "content-monetization", "content-scoring", "content-humanizer"
        };

        foreach (var agent in canonicalAgents)
        {
            var task = new WorkflowTaskTemplate { AssignedAgent = agent };
            task.AssignedAgent.Should().Be(agent);
        }
    }
}