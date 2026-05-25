using ContentOS.Application.Abstractions;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowHardeningTests
{
    private ContentOsDbContext _dbContext = null!;
    private IWorkflowMutationService _mutationService = null!;
    private IWorkflowControlService _controlService = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _mutationService = new WorkflowMutationService(_dbContext);
        _controlService = new WorkflowControlService(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task InjectManualTask_should_block_insertion_after_running_task()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.InProgress);

        var act = () => _mutationService.InjectManualTaskAsync(seed.JobId, new InjectWorkflowTaskRequestDto
        {
            Name = "Manual Review",
            InsertAfterTaskId = seed.FirstTaskId,
            CreatedBy = "Eric",
            Reason = "Need human check"
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cannot inject after a running task*");
    }

    [Test]
    public async Task ExecuteTask_should_block_rejected_task_without_reset()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Rejected);

        var act = () => _controlService.ExecuteTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric" });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Rejected tasks require an explicit reset/rerun action*");
    }

    [Test]
    public async Task AcceptTask_should_block_when_result_artifact_missing()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed, firstTaskHasResult: false);

        var act = () => _controlService.AcceptTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric" });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*without a produced result*");
    }

    [Test]
    public async Task ResetTask_should_ready_rejected_task_and_increment_attempts()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Rejected);

        var result = await _controlService.ResetTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto { CreatedBy = "Eric", Reason = "Retry" });

        var task = await _dbContext.ContentWorkflowTasks.FirstAsync(x => x.Id == seed.FirstTaskId);
        result.TaskStatus.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready.ToString());
        task.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        task.ExecutionAttemptCount.Should().Be(1);
    }

    [Test]
    public async Task Mutations_should_create_workflow_mutation_logs()
    {
        var seed = await SeedWorkflowAsync(firstTaskStatus: ContentOS.Domain.Enums.TaskStatus.Completed);

        await _controlService.RejectTaskAsync(seed.JobId, seed.FirstTaskId, new WorkflowTaskControlRequestDto
        {
            CreatedBy = "Eric",
            Reason = "Needs revision",
            Source = "User",
            CorrelationId = "corr-123"
        });

        var log = await _dbContext.WorkflowMutationLogs.OrderByDescending(x => x.CreatedUtc).FirstAsync();
        log.MutationType.Should().Be(ContentOS.Domain.Enums.WorkflowMutationType.RejectTask);
        log.ActorIdentity.Should().Be("Eric");
        log.ActorType.Should().Be("User");
        log.Reason.Should().Be("Needs revision");
        log.CorrelationId.Should().Be("corr-123");
        log.PreviousStatus.Should().NotBeNull();
        log.NewStatus.Should().NotBeNull();
    }

    private async Task<(Guid JobId, Guid FirstTaskId)> SeedWorkflowAsync(
        string jobStatus = "InProgress",
        ContentOS.Domain.Enums.TaskStatus firstTaskStatus = ContentOS.Domain.Enums.TaskStatus.Ready,
        bool firstTaskHasResult = true)
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
            StartedUtc = DateTime.UtcNow
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
            ResultArtifactId = firstTaskHasResult ? Guid.NewGuid().ToString() : null,
            InputDataJson = "{}"
        };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        _dbContext.ContentWorkflowTasks.Add(firstTask);
        await _dbContext.SaveChangesAsync();

        return (job.Id, firstTask.Id);
    }
}
