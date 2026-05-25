using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ContentOS.Api.Tests;

[Collection("ApiIntegration")]
public class WorkflowMutationsApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WorkflowMutationsApiIntegrationTests()
    {
        _factory = new ApiWebApplicationFactory();
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiWebApplicationFactory.GenerateJwtToken());
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ReorderWorkflowTask_endpoint_reorders_tasks_and_persists_audit_log()
    {
        var seed = await SeedWorkflowAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/content-generation/workflows/{seed.JobId}/tasks/reorder", new
        {
            taskId = seed.ThirdTaskId,
            newDisplayOrder = 1,
            createdBy = "Eric",
            reason = "Move QA earlier",
            source = "User",
            actorType = "User",
            correlationId = "api-reorder-1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.VerifyAsync(async db =>
        {
            var tasks = db.ContentWorkflowTasks.Where(x => x.ContentWorkflowJobId == seed.JobId).OrderBy(x => x.DisplayOrder).ToList();
            tasks[0].Id.Should().Be(seed.ThirdTaskId);

            var log = db.WorkflowMutationLogs.OrderByDescending(x => x.CreatedUtc).First();
            log.MutationType.Should().Be(WorkflowMutationType.ReorderTask);
            log.ActorIdentity.Should().Be("Eric");
            log.ActorType.Should().Be("User");
            log.CorrelationId.Should().Be("api-reorder-1");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ReopenWorkflow_endpoint_reopens_completed_workflow()
    {
        var seed = await SeedWorkflowAsync(jobStatus: "Completed", secondStatus: ContentOS.Domain.Enums.TaskStatus.Rejected);

        var response = await _client.PostAsJsonAsync($"/api/v1/content-generation/workflows/{seed.JobId}/reopen", new
        {
            createdBy = "Eric",
            reason = "Need another pass",
            source = "User",
            actorType = "User",
            correlationId = "api-reopen-1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();

        await _factory.VerifyAsync(async db =>
        {
            var job = await db.ContentWorkflowJobs.FindAsync(seed.JobId);
            var rejected = await db.ContentWorkflowTasks.FindAsync(seed.SecondTaskId);
            job!.Status.Should().Be("InProgress");
            rejected!.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
        });
    }

    private async Task<(Guid JobId, Guid FirstTaskId, Guid SecondTaskId, Guid ThirdTaskId)> SeedWorkflowAsync(
        string jobStatus = "InProgress",
        ContentOS.Domain.Enums.TaskStatus secondStatus = ContentOS.Domain.Enums.TaskStatus.Pending)
    {
        var jobId = Guid.NewGuid();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        var thirdTaskId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Workflow Site", Domain = "workflow.test" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Workflow Idea", PrimaryKeyword = "workflow" });
            db.WorkflowTemplates.Add(new WorkflowTemplate { Id = templateId, Name = $"Template-{jobId}", Description = "Template", Version = 1 });
            db.ContentWorkflowJobs.Add(new ContentWorkflowJob
            {
                Id = jobId,
                ContentIdeaId = ideaId,
                WorkflowTemplateId = templateId,
                Status = jobStatus,
                CurrentStage = "Writing",
                StartedUtc = DateTime.UtcNow,
                CompletedUtc = jobStatus == "Completed" ? DateTime.UtcNow : null
            });
            db.ContentWorkflowTasks.AddRange(
                new ContentWorkflowTask
                {
                    Id = firstTaskId,
                    ContentWorkflowJobId = jobId,
                    Name = "Write Draft",
                    StageName = "Writing",
                    DisplayOrder = 1,
                    AssignedAgent = "writer",
                    Status = ContentOS.Domain.Enums.TaskStatus.Completed,
                    ScopeType = TargetScopeType.Global,
                    ResultArtifactId = Guid.NewGuid().ToString()
                },
                new ContentWorkflowTask
                {
                    Id = secondTaskId,
                    ContentWorkflowJobId = jobId,
                    Name = "SEO Review",
                    StageName = "SEO",
                    DisplayOrder = 2,
                    AssignedAgent = "seo",
                    Status = secondStatus,
                    ScopeType = TargetScopeType.Section,
                    ScopeId = "intro"
                },
                new ContentWorkflowTask
                {
                    Id = thirdTaskId,
                    ContentWorkflowJobId = jobId,
                    Name = "QA Review",
                    StageName = "QA",
                    DisplayOrder = 3,
                    AssignedAgent = "qa",
                    Status = ContentOS.Domain.Enums.TaskStatus.Pending,
                    ScopeType = TargetScopeType.Section,
                    ScopeId = "faq"
                });
        });

        return (jobId, firstTaskId, secondTaskId, thirdTaskId);
    }
}
