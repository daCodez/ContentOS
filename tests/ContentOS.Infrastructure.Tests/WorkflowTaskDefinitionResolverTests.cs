using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using FluentAssertions;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowTaskDefinitionResolverTests
{
    private WorkflowTaskDefinitionResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _resolver = new WorkflowTaskDefinitionResolver();
    }

    [Test]
    public void Resolve_should_prefer_assigned_agent_mapping()
    {
        var task = new ContentWorkflowTask
        {
            Name = "Some Legacy Name",
            AssignedAgent = AgentStack.WriterAgent,
            Kind = TaskKind.Template,
            ScopeType = TargetScopeType.Global
        };

        var result = _resolver.Resolve(task);

        result.ExecutionKey.Should().Be("draft-article");
        result.ArtifactType.Should().Be("DraftArticle");
        result.Summary.Should().Contain("assigned agent");
        result.Warnings.Should().BeNull();
    }

    [Test]
    public void Resolve_should_fallback_to_legacy_task_name()
    {
        var task = new ContentWorkflowTask
        {
            Name = "Run Light QA Check",
            AssignedAgent = string.Empty,
            Kind = TaskKind.Template,
            ScopeType = TargetScopeType.Global
        };

        var result = _resolver.Resolve(task);

        result.ExecutionKey.Should().Be("light-qa");
        result.ArtifactType.Should().Be("LightQaReport");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("legacy task-name routing fallback");
    }

    [Test]
    public void Resolve_should_use_default_route_when_no_match_exists()
    {
        var task = new ContentWorkflowTask
        {
            Name = "Unknown Task",
            AssignedAgent = string.Empty,
            Kind = TaskKind.Manual,
            ScopeType = TargetScopeType.Section,
            ScopeId = "intro"
        };

        var result = _resolver.Resolve(task);

        result.ExecutionKey.Should().Be("default");
        result.ArtifactType.Should().Be("WorkflowArtifact");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("fell back to default");
    }
}
