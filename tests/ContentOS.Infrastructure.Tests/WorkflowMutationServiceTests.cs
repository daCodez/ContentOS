using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowMutationServiceTests
{
    private ContentOsDbContext _dbContext = null!;
    private IWorkflowMutationService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _service = new WorkflowMutationService(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task InjectManualTaskAsync_should_insert_after_requested_task_and_shift_following_tasks()
    {
        var seed = await SeedWorkflowAsync();

        var result = await _service.InjectManualTaskAsync(seed.JobId, new InjectWorkflowTaskRequestDto
        {
            Name = "Manual Fact Check",
            StageName = "Review",
            AssignedAgent = "manual-review",
            ScopeType = "Section",
            ScopeId = "intro",
            InsertAfterTaskId = seed.FirstTaskId
        });

        var tasks = await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == seed.JobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        result.DisplayOrder.Should().Be(2);
        result.Kind.Should().Be(TaskKind.Manual.ToString());
        result.ScopeType.Should().Be(TargetScopeType.Section.ToString());
        tasks.Should().HaveCount(3);
        tasks[1].Name.Should().Be("Manual Fact Check");
        tasks[1].InsertedAfterTaskId.Should().Be(seed.FirstTaskId);
        tasks[2].DisplayOrder.Should().Be(3);
    }

    [Test]
    public async Task InjectManualTaskAsync_should_mark_manual_task_ready_when_inserted_after_completed_task()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed, secondTaskStatus: ContentOS.Domain.Enums.TaskStatus.Pending, jobStatus: "Completed");

        var result = await _service.InjectManualTaskAsync(seed.JobId, new InjectWorkflowTaskRequestDto
        {
            Name = "Manual SEO Review",
            StageName = "Review",
            AssignedAgent = "manual-review",
            InsertAfterTaskId = seed.FirstTaskId
        });

        var insertedTask = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == result.TaskId);
        var job = await _dbContext.ContentWorkflowJobs.FirstAsync(x => x.Id == seed.JobId);

        insertedTask.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        job.Status.Should().Be("InProgress");
        job.CurrentStage.Should().Be("Review");
    }

    private async Task<(Guid JobId, Guid FirstTaskId)> SeedWorkflowAsync(
        ContentOS.Domain.Enums.TaskStatus firstTaskStatus = ContentOS.Domain.Enums.TaskStatus.Ready,
        ContentOS.Domain.Enums.TaskStatus secondTaskStatus = ContentOS.Domain.Enums.TaskStatus.Pending,
        string jobStatus = "InProgress")
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test Site", Domain = "example.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Title", PrimaryKeyword = "keyword" };
        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "Template", Description = "Template", Version = 1 };
        var job = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(),
            ContentIdeaId = idea.Id,
            WorkflowTemplateId = template.Id,
            Status = jobStatus,
            CurrentStage = "Writing",
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = jobStatus == "Completed" ? DateTime.UtcNow : null
        };

        var firstTask = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            Name = "Write Rule-Compliant Draft",
            StageName = "Writing",
            DisplayOrder = 1,
            AssignedAgent = "content-writer",
            Status = firstTaskStatus,
            ScopeType = TargetScopeType.Global
        };

        var secondTask = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            Name = "Run Strict QA Scoring",
            StageName = "QA",
            DisplayOrder = 2,
            AssignedAgent = "content-scoring",
            Status = secondTaskStatus,
            ScopeType = TargetScopeType.Global
        };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        _dbContext.ContentWorkflowTasks.AddRange(firstTask, secondTask);
        await _dbContext.SaveChangesAsync();

        return (job.Id, firstTask.Id);
    }
}
