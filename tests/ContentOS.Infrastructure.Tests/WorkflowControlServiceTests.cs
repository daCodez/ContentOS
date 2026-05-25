using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowControlServiceTests
{
    private ContentOsDbContext _dbContext = null!;
    private IWorkflowControlService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _service = new WorkflowControlService(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task ExecuteTaskAsync_should_make_pending_task_ready_and_reopen_job()
    {
        var seed = await SeedWorkflowAsync(jobStatus: "Paused", firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Pending);

        var result = await _service.ExecuteTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric", Reason = "Run now" });

        var task = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == seed.FirstTaskId);
        var job = await _dbContext.ContentWorkflowJobs.FirstAsync(x => x.Id == seed.JobId);

        result.TaskStatus.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready.ToString());
        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        job.Status.Should().Be("InProgress");
    }

    [Test]
    public async Task AcceptTaskAsync_should_advance_next_pending_task()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed, secondTaskStatus: ContentOS.Domain.Enums.TaskStatus.Pending);

        var result = await _service.AcceptTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric" });

        var nextTask = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == seed.SecondTaskId);
        var job = await _dbContext.ContentWorkflowJobs.FirstAsync(x => x.Id == seed.JobId);

        result.JobStatus.Should().Be("InProgress");
        nextTask.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        job.CurrentStage.Should().Be("QA");
    }

    [Test]
    public async Task RejectTaskAsync_should_block_job_and_mark_task_rejected()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed);

        var result = await _service.RejectTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { Reason = "Needs review" });

        var task = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == seed.FirstTaskId);
        var job = await _dbContext.ContentWorkflowJobs.FirstAsync(x => x.Id == seed.JobId);

        result.JobStatus.Should().Be("Blocked");
        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Rejected);
        job.ErrorMessage.Should().Be("Needs review");
    }

    [Test]
    public async Task PauseAndResumeWorkflow_should_toggle_job_and_ready_next_task()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed, secondTaskStatus: ContentOS.Domain.Enums.TaskStatus.Pending);

        await _service.PauseWorkflowAsync(seed.JobId, new WorkflowTaskControlRequestDto { Reason = "Human review" });
        var resume = await _service.ResumeWorkflowAsync(seed.JobId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric" });

        var job = await _dbContext.ContentWorkflowJobs.FirstAsync(x => x.Id == seed.JobId);
        var nextTask = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == seed.SecondTaskId);

        job.Status.Should().Be("InProgress");
        nextTask.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        resume.TaskId.Should().Be(seed.SecondTaskId);
    }

    [Test]
    public async Task ExecuteTaskAsync_should_block_cancelled_jobs()
    {
        var seed = await SeedWorkflowAsync(jobStatus: "Cancelled", firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Pending);

        var act = () => _service.ExecuteTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cancelled workflows cannot be changed*");
    }

    private async Task<(Guid JobId, Guid FirstTaskId, Guid SecondTaskId)> SeedWorkflowAsync(
        string jobStatus = "InProgress",
        ContentOS.Domain.Enums.TaskStatus firstTaskStatus = ContentOS.Domain.Enums.TaskStatus.Ready,
        ContentOS.Domain.Enums.TaskStatus secondTaskStatus = ContentOS.Domain.Enums.TaskStatus.Pending)
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
            ScopeType = TargetScopeType.Global,
            ResultArtifactId = firstTaskStatus == ContentOS.Domain.Enums.TaskStatus.Completed ? Guid.NewGuid().ToString() : null,
            InputDataJson = "{}"
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
            ScopeType = TargetScopeType.Global,
            InputDataJson = "{}"
        };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        _dbContext.ContentWorkflowTasks.AddRange(firstTask, secondTask);
        await _dbContext.SaveChangesAsync();

        return (job.Id, firstTask.Id, secondTask.Id);
    }
}
