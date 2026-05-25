using ContentOS.Infrastructure.Agents;
using FluentAssertions;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowTaskRoutingTests
{
    [TestCase("Research Topic and Intent", "research-intent")]
    [TestCase("Build Keyword Strategy", "keyword-strategy")]
    [TestCase("Humanize Final Draft", "humanize-final")]
    public void TaskNameToExecutionKey_should_map_known_tasks(string taskName, string executionKey)
    {
        WorkflowTaskRouting.TaskNameToExecutionKey.TryGetValue(taskName, out var resolved).Should().BeTrue();
        resolved.Should().Be(executionKey);
    }

    [TestCase(AgentStack.WriterAgent, "draft-article")]
    [TestCase(AgentStack.QaScoringAgent, "strict-qa")]
    [TestCase(AgentStack.LightQaAgent, "light-qa")]
    public void AgentExecutionKeys_should_map_known_agents(string agent, string executionKey)
    {
        WorkflowTaskRouting.AgentExecutionKeys.TryGetValue(agent, out var resolved).Should().BeTrue();
        resolved.Should().Be(executionKey);
    }

    [TestCase("draft-article", "DraftArticle")]
    [TestCase("strict-qa", "QaReport")]
    [TestCase("default", "WorkflowArtifact")]
    public void ExecutionKeyToArtifactType_should_map_known_execution_keys(string executionKey, string artifactType)
    {
        WorkflowTaskRouting.ExecutionKeyToArtifactType.TryGetValue(executionKey, out var resolved).Should().BeTrue();
        resolved.Should().Be(artifactType);
    }
}
