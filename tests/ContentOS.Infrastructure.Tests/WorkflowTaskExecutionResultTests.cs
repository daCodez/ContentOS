using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using FluentAssertions;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowTaskExecutionResultTests
{
    [Test]
    public void Constructor_should_capture_execution_metadata_and_artifact_ids()
    {
        var primary = new ContentArtifact { Id = Guid.NewGuid(), ArtifactType = "DraftArticle" };
        var preview = new ContentArtifact { Id = Guid.NewGuid(), ArtifactType = "FinalArticlePreview" };
        var supplemental = new[]
        {
            new ContentArtifact { Id = Guid.NewGuid(), ArtifactType = "QaReport" },
            new ContentArtifact { Id = Guid.NewGuid(), ArtifactType = "PublishReadyPackage" }
        };

        var result = new WorkflowTaskExecutionResult(
            Artifact: primary,
            SupplementalArtifacts: supplemental,
            FinalArticlePreviewArtifact: preview,
            ResolvedExecutionKey: "draft-article",
            ExecutionSummary: "resolved via assigned agent",
            Warnings: new[] { "legacy fallback not used" });

        result.PrimaryArtifactId.Should().Be(primary.Id);
        result.PreviewArtifactId.Should().Be(preview.Id);
        result.SupplementalArtifactIds.Should().BeEquivalentTo(supplemental.Select(x => x.Id));
        result.ResolvedExecutionKey.Should().Be("draft-article");
        result.ExecutionSummary.Should().Be("resolved via assigned agent");
        result.Warnings.Should().ContainSingle();
    }
}
