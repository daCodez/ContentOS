using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowExportServiceTests
{
    private ContentOsDbContext _dbContext = null!;
    private IWorkflowExportService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _service = new WorkflowExportService(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task ExportAsync_should_return_workflow_snapshot_with_execution_metadata()
    {
        var jobId = await SeedWorkflowAsync();

        var export = await _service.ExportAsync(jobId);

        export.Should().NotBeNull();
        export!.WorkflowJobId.Should().Be(jobId);
        export.Tasks.Should().HaveCount(2);
        var firstTask = export.Tasks.First();
        firstTask.ResolvedExecutionKey.Should().Be("draft-article");
        firstTask.ExecutionSummary.Should().Be("resolved via assigned agent");
        firstTask.Warnings.Should().Contain("legacy fallback not used");
        firstTask.PreviewArtifactId.Should().NotBeNull();
        firstTask.SupplementalArtifactIds.Should().HaveCount(2);
    }

    [Test]
    public async Task ExportMarkdownAsync_should_render_useful_workflow_summary()
    {
        var jobId = await SeedWorkflowAsync();

        var markdown = await _service.ExportMarkdownAsync(jobId);

        markdown.Should().NotBeNull();
        markdown.Should().Contain("# Workflow Export");
        markdown.Should().Contain("### 1. Write Rule-Compliant Draft");
        markdown.Should().Contain("Resolved Execution Key: draft-article");
        markdown.Should().Contain("Execution Summary: resolved via assigned agent");
        markdown.Should().Contain("Warnings: legacy fallback not used");
    }

    private async Task<Guid> SeedWorkflowAsync()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test Site", Domain = "example.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Title", PrimaryKeyword = "keyword" };
        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "Template", Description = "Template", Version = 1 };
        var job = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(),
            ContentIdeaId = idea.Id,
            WorkflowTemplateId = template.Id,
            Status = "InProgress",
            CurrentStage = "Writing",
            StartedUtc = DateTime.UtcNow
        };

        var previewId = Guid.NewGuid();
        var supplementalOne = Guid.NewGuid();
        var supplementalTwo = Guid.NewGuid();

        var task = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            Name = "Write Rule-Compliant Draft",
            StageName = "Writing",
            DisplayOrder = 1,
            AssignedAgent = AgentStack.WriterAgent,
            Status = ContentOS.Domain.Enums.TaskStatus.Completed,
            ScopeType = TargetScopeType.Global,
            ResultArtifactId = Guid.NewGuid().ToString(),
            InputDataJson = JsonSerializer.Serialize(new
            {
                resolvedExecutionKey = "draft-article",
                executionSummary = "resolved via assigned agent",
                warnings = new[] { "legacy fallback not used" },
                previewArtifactId = previewId,
                supplementalArtifactIds = new[] { supplementalOne, supplementalTwo }
            })
        };

        var pendingTask = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            Name = "Run Strict QA Scoring",
            StageName = "QA",
            DisplayOrder = 2,
            AssignedAgent = AgentStack.QaScoringAgent,
            Status = ContentOS.Domain.Enums.TaskStatus.Pending,
            ScopeType = TargetScopeType.Section,
            ScopeId = "faq"
        };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        _dbContext.ContentWorkflowTasks.AddRange(task, pendingTask);
        await _dbContext.SaveChangesAsync();

        return job.Id;
    }
}
