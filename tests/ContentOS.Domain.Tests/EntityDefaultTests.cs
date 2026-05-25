using Xunit;
using ContentOS.Domain.Entities;
using FluentAssertions;

namespace ContentOS.Domain.Tests;

public class SiteTests
{
    [Fact]
    public void Site_DefaultValues_AreSetCorrectly()
    {
        var site = new Site();

        site.Id.Should().Be(Guid.Empty);
        site.Name.Should().BeEmpty();
        site.Domain.Should().BeEmpty();
        site.Niche.Should().BeEmpty();
        site.PlatformType.Should().Be("WordPress");
        site.IsActive.Should().BeTrue();
        site.DefaultTone.Should().BeEmpty();
        site.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        site.UpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class ContentArtifactTests
{
    [Fact]
    public void ContentArtifact_DefaultValues_AreSetCorrectly()
    {
        var artifact = new ContentArtifact();

        artifact.Id.Should().Be(Guid.Empty);
        artifact.ContentWorkflowJobId.Should().Be(Guid.Empty);
        artifact.ContentWorkflowTaskId.Should().BeNull();
        artifact.ArtifactType.Should().BeEmpty();
        artifact.Title.Should().BeEmpty();
        artifact.StorageType.Should().Be("Database");
        artifact.ContentJson.Should().Be("{}");
        artifact.ContentText.Should().BeEmpty();
        artifact.BlobPath.Should().BeEmpty();
        artifact.VersionNumber.Should().Be(1);
        artifact.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        artifact.CreatedByAgent.Should().BeEmpty();
    }
}

public class ContentResearchSourceTests
{
    [Fact]
    public void ContentResearchSource_DefaultValues_AreSetCorrectly()
    {
        var source = new ContentResearchSource();

        source.Id.Should().Be(Guid.Empty);
        source.ContentIdeaId.Should().Be(Guid.Empty);
        source.SourceType.Should().BeEmpty();
        source.SourceTitle.Should().BeEmpty();
        source.SourceUrl.Should().BeEmpty();
        source.Notes.Should().BeEmpty();
        source.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class AgentExecutionLogTests
{
    [Fact]
    public void AgentExecutionLog_DefaultValues_AreSetCorrectly()
    {
        var log = new AgentExecutionLog();

        log.Id.Should().Be(Guid.Empty);
        log.ContentWorkflowJobId.Should().Be(Guid.Empty);
        log.ContentWorkflowTaskId.Should().BeNull();
        log.AgentName.Should().BeEmpty();
        log.Level.Should().Be("Info");
        log.Message.Should().BeEmpty();
        log.DetailsJson.Should().Be("{}");
        log.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}