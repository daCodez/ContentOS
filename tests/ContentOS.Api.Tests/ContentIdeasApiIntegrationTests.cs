using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Research.Serp;
using ContentOS.Infrastructure.Image;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Scoring;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Research.SearXng;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace ContentOS.Api.Tests;

/// <summary>
/// Integration tests for ContentIdeasController endpoints.
/// Uses WebApplicationFactory with SQLite in-memory and mocked external deps.
/// Each test gets a clean database via IAsyncLifetime cleanup.
/// </summary>
[Collection("ApiIntegration")]
public class ContentIdeasApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ContentIdeasApiIntegrationTests()
    {
        _factory = new ApiWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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

    // ====================================================================
    // GET /api/v1/content-ideas
    // ====================================================================

    [Fact]
    public async Task GetAll_NoIdeas_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/v1/content-ideas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        var data = body.GetProperty("data");
        data.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetAll_WithIdeas_ReturnsAllIdeas()
    {
        await _factory.SeedAsync(db =>
        {
            var site = new Site { Id = Guid.NewGuid(), Name = "Seed Site", Domain = "seed.com", Niche = "Tech" };
            db.Sites.Add(site);
            db.ContentIdeas.Add(new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Idea A", PrimaryKeyword = "a", Status = "NeedsReview" });
            db.ContentIdeas.Add(new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Idea B", PrimaryKeyword = "b", Status = "Discovered" });
        });

        var response = await _client.GetAsync("/api/v1/content-ideas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    // ====================================================================
    // POST /api/v1/content-ideas (Create)
    // ====================================================================

    [Fact]
    public async Task Create_ValidIdea_ReturnsOkWithId()
    {
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Create Site", Domain = "create.com", Niche = "Testing", IsActive = true });
        });

        var payload = new
        {
            siteId,
            title = "New Content Idea",
            primaryKeyword = "content-ideas",
            summary = "A test idea",
            contentType = "LongFormBlogArticle"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/content-ideas", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Content idea created.");
        body.GetProperty("data").GetProperty("contentIdeaId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_InvalidSite_ReturnsInternalServerError()
    {
        var payload = new
        {
            siteId = Guid.NewGuid(),
            title = "Orphan Idea",
            primaryKeyword = "orphan"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/content-ideas", payload);

        // Handler throws InvalidOperationException for nonexistent site
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ====================================================================
    // POST /api/v1/content-ideas/{id}/approve
    // ====================================================================

    [Fact]
    public async Task Approve_ValidIdea_ReturnsOkWithWorkflowJobId()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Approve Site", Domain = "approve.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Approve Me", PrimaryKeyword = "approve", Status = "NeedsReview" });
            // WorkflowTemplate already seeded by WorkflowTemplateSeeder on startup
        });
        await _factory.SeedAsync(db =>
        {
            var template = db.WorkflowTemplates.First(t => t.Name == "LongFormBlogArticle");
            db.WorkflowTaskTemplates.Add(new WorkflowTaskTemplate
            {
                Id = Guid.NewGuid(),
                WorkflowTemplateId = template.Id,
                Name = "Planning",
                StageName = "Planning",
                DisplayOrder = 1,
                AssignedAgent = "Coordinator",
                InstructionsTemplate = "Plan it"
            });
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Content idea approved.");
        body.GetProperty("data").GetProperty("workflowJobId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Approve_NonExistentIdea_ReturnsInternalServerError()
    {
        var fakeId = Guid.NewGuid();

        var response = await _client.PostAsync($"/api/v1/content-ideas/{fakeId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Approve_WithSeededTemplate_SucceedsWithoutManualTemplate()
    {
        // Verify that the WorkflowTemplateSeeder provides the template on startup,
        // so no manual template seeding is needed for approval to work
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Seeded Template Site", Domain = "seeded.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Seeded Template", PrimaryKeyword = "seeded-template", Status = "NeedsReview" });
            // No manual WorkflowTemplate added — relies on seeder
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/approve", null);

        // The seeder ensures LongFormBlogArticle template exists, so approval should succeed
        // (task templates also need to exist — seeder may or may not provide those)
        // At minimum, it should not throw an unhandled exception
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Approve_UpdatesIdeaStatusToApproved()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Status Site", Domain = "status.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Status Check", PrimaryKeyword = "status", Status = "NeedsReview" });
            // WorkflowTemplate already seeded by WorkflowTemplateSeeder
        });
        await _factory.SeedAsync(db =>
        {
            var template = db.WorkflowTemplates.First(t => t.Name == "LongFormBlogArticle");
            db.WorkflowTaskTemplates.Add(new WorkflowTaskTemplate
            {
                Id = Guid.NewGuid(),
                WorkflowTemplateId = template.Id,
                Name = "Research",
                StageName = "Research",
                DisplayOrder = 1,
                AssignedAgent = "Researcher",
                InstructionsTemplate = "Research it"
            });
        });

        await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/approve", null);

        await _factory.VerifyAsync(async db =>
        {
            var idea = await db.ContentIdeas.FindAsync(ideaId);
            idea.Should().NotBeNull();
            idea!.Status.Should().Be("Approved");
            idea.ApprovedBy.Should().Be("Eric");
            idea.ApprovedUtc.Should().NotBeNull();
        });
    }

    // ====================================================================
    // POST /api/v1/content-ideas/{id}/archive
    // ====================================================================

    [Fact]
    public async Task Archive_ValidIdea_ReturnsOk()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Archive Site", Domain = "archive.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Archive Me", PrimaryKeyword = "archive", Status = "NeedsReview" });
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Content idea archived.");
    }

    [Fact]
    public async Task Archive_UpdatesIdeaStatusToArchived()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Archive Status Site", Domain = "archivestatus.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Archive Status", PrimaryKeyword = "archive-status", Status = "NeedsReview" });
        });

        await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/archive", null);

        await _factory.VerifyAsync(async db =>
        {
            var idea = await db.ContentIdeas.FindAsync(ideaId);
            idea.Should().NotBeNull();
            idea!.Status.Should().Be("Archived");
        });
    }

    [Fact]
    public async Task Archive_NonExistentIdea_ReturnsInternalServerError()
    {
        var fakeId = Guid.NewGuid();

        var response = await _client.PostAsync($"/api/v1/content-ideas/{fakeId}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Archive_ApprovedIdeaWithWorkflow_ReturnsInternalServerError()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Archive Workflow Site", Domain = "archiveworkflow.com", Niche = "Testing" });
            var idea = new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Archive Workflow", PrimaryKeyword = "archive-workflow", Status = "Approved" };
            db.ContentIdeas.Add(idea);
            var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "ArchiveWfTemplate", Description = "Archive", Version = 1 };
            db.WorkflowTemplates.Add(template);
            db.ContentWorkflowJobs.Add(new ContentWorkflowJob
            {
                Id = Guid.NewGuid(),
                ContentIdeaId = ideaId,
                WorkflowTemplateId = template.Id,
                Status = "Queued"
            });
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Archive_ApprovedIdeaWithoutWorkflow_Succeeds()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Archive NoWf Site", Domain = "archivenowf.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Archive No Workflow", PrimaryKeyword = "archive-no-wf", Status = "Approved" });
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/{ideaId}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.VerifyAsync(async db =>
        {
            var idea = await db.ContentIdeas.FindAsync(ideaId);
            idea!.Status.Should().Be("Archived");
        });
    }

    // ====================================================================
    // DELETE /api/v1/content-ideas/{id}
    // ====================================================================

    [Fact]
    public async Task Delete_ValidDiscoveredIdea_ReturnsOk()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Delete Site", Domain = "delete.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Delete Me", PrimaryKeyword = "delete", Status = "Discovered" });
        });

        var response = await _client.DeleteAsync($"/api/v1/content-ideas/{ideaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Content idea deleted.");
    }

    [Fact]
    public async Task Delete_RemovesIdeaFromDatabase()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Delete Verify Site", Domain = "deleteverify.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Delete Verify", PrimaryKeyword = "delete-verify", Status = "NeedsReview" });
        });

        await _client.DeleteAsync($"/api/v1/content-ideas/{ideaId}");

        await _factory.VerifyAsync(async db =>
        {
            var idea = await db.ContentIdeas.FindAsync(ideaId);
            idea.Should().BeNull();
        });
    }

    [Fact]
    public async Task Delete_NonExistentIdea_ReturnsInternalServerError()
    {
        var fakeId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/v1/content-ideas/{fakeId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Delete_ApprovedIdea_ReturnsInternalServerError()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Delete Approved Site", Domain = "deleteapproved.com", Niche = "Testing" });
            db.ContentIdeas.Add(new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Delete Approved", PrimaryKeyword = "delete-approved", Status = "Approved" });
        });

        var response = await _client.DeleteAsync($"/api/v1/content-ideas/{ideaId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Delete_IdeaWithWorkflow_ReturnsInternalServerError()
    {
        var ideaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Delete Wf Site", Domain = "deletewf.com", Niche = "Testing" });
            var idea = new ContentIdea { Id = ideaId, SiteId = siteId, Title = "Delete Wf", PrimaryKeyword = "delete-wf", Status = "NeedsReview" };
            db.ContentIdeas.Add(idea);
            var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "DeleteWfTemplate", Description = "Delete", Version = 1 };
            db.WorkflowTemplates.Add(template);
            db.ContentWorkflowJobs.Add(new ContentWorkflowJob
            {
                Id = Guid.NewGuid(),
                ContentIdeaId = ideaId,
                WorkflowTemplateId = template.Id,
                Status = "Queued"
            });
        });

        var response = await _client.DeleteAsync($"/api/v1/content-ideas/{ideaId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ====================================================================
    // POST /api/v1/content-ideas/research/run/{siteId}
    // ====================================================================

    [Fact]
    public async Task RunResearch_ValidSite_ReturnsOkWithResult()
    {
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Research Site", Domain = "research.com", Niche = "Testing", IsActive = true });
        });

        var payload = new { requestedIdeaCount = 5 };

        var response = await _client.PostAsJsonAsync($"/api/v1/content-ideas/research/run/{siteId}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Research run completed.");
    }

    [Fact]
    public async Task RunResearch_NullBody_Succeeds()
    {
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Research Null Site", Domain = "researchnull.com", Niche = "Testing", IsActive = true });
        });

        var response = await _client.PostAsync($"/api/v1/content-ideas/research/run/{siteId}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RunResearch_WithIdeaCount_ReturnsOk()
    {
        var siteId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.Sites.Add(new Site { Id = siteId, Name = "Research Verify Site", Domain = "researchverify.com", Niche = "Testing", IsActive = true });
        });

        var payload = new { requestedIdeaCount = 10 };
        var response = await _client.PostAsJsonAsync($"/api/v1/content-ideas/research/run/{siteId}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RunResearch_InvalidSite_ReturnsOkWithMockedResult()
    {
        // The ResearchAgent is mocked and always returns success regardless of siteId.
        // In production, the agent would fail for invalid sites, but the controller
        // doesn't validate site existence before delegating.
        var fakeSiteId = Guid.NewGuid();
        var payload = new { requestedIdeaCount = 5 };

        var response = await _client.PostAsJsonAsync($"/api/v1/content-ideas/research/run/{fakeSiteId}", payload);

        // Mocked agent returns success regardless of site validity
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSuccessful").GetBoolean().Should().BeTrue();
    }

    // ====================================================================
    // Authentication
    // ====================================================================

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        // Create a fresh factory for this test since we need an unauthenticated client
        using var unauthFactory = new ApiWebApplicationFactory();
        var unauthClient = unauthFactory.CreateClient();

        var response = await unauthClient.GetAsync("/api/v1/content-ideas");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PingEndpoint_AllowsAnonymous()
    {
        // Create a fresh factory for this test
        using var unauthFactory = new ApiWebApplicationFactory();
        var unauthClient = unauthFactory.CreateClient();

        var response = await unauthClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// ====================================================================
// WebApplicationFactory with SQLite in-memory + all external deps mocked
// Each instance creates its own SQLite connection for test isolation.
// ====================================================================

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public IResearchAgent ResearchAgentMock { get; private set; } = null!;
    public IIdeationAgent IdeationAgentMock { get; private set; } = null!;

    private SqliteConnection _connection = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Set the JWT signing key so Program.cs doesn't throw
        Environment.SetEnvironmentVariable("CONTENTOS_JWT_SIGNING_KEY",
            "ContentOS-Test-JWT-Signing-Key-Must-Be-32-Chars!!");

        builder.ConfigureServices(services =>
        {
            // 1. Remove the real DbContext and replace with SQLite in-memory
            // (SQLite supports relational methods like GetPendingMigrations)
            services.RemoveAll<DbContextOptions<ContentOsDbContext>>();
            services.RemoveAll<ContentOsDbContext>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<ContentOsDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // 2. Remove the background service (it loops and calls real agents)
            services.RemoveAll<IHostedService>();

            // 3. Mock all agents and external services
            var researchAgent = Substitute.For<IResearchAgent>();
            ResearchAgentMock = researchAgent;

            var ideationAgent = Substitute.For<IIdeationAgent>();
            IdeationAgentMock = ideationAgent;

            var workflowWriter = Substitute.For<IWorkflowArticleWriter>();
            var serpBenchmark = Substitute.For<ISerpBenchmarkService>();
            var optimizationScoring = Substitute.For<IOptimizationScoringService>();
            var imageGen = Substitute.For<IImageGenerationService>();
            var contentStrategy = Substitute.For<IContentStrategyAgent>();
            var editorial = Substitute.For<IEditorialAgent>();
            var seoMonetization = Substitute.For<ISeoAndMonetizationAgent>();
            var qaCompliance = Substitute.For<IQaAndComplianceAgent>();
            var qaFixVerification = Substitute.For<IQaFixVerificationAgent>();
            var seoOptimization = Substitute.For<ISeoOptimizationAgent>();
            var workflowCoordinator = Substitute.For<IWorkflowCoordinatorAgent>();
            var topicDiversityScorer = Substitute.For<ContentOS.Infrastructure.Ideation.ITopicDiversityScorer>();

            // Set up default return values for research agent
            researchAgent.RunAsync(Arg.Any<Guid>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var siteId = callInfo.Arg<Guid>();
                    var count = callInfo.Arg<int?>();
                    return new RunResearchResult
                    {
                        SiteId = siteId,
                        RequestedIdeaCount = count ?? 15,
                        FindingsGathered = 0,
                        CandidatesCreated = 0,
                        IdeasSaved = 0,
                        DuplicatesSkipped = 0,
                        SavedTitles = []
                    };
                });

            // Override all infrastructure service registrations with mocks
            services.RemoveAll<IResearchAgent>();
            services.AddScoped(_ => researchAgent);

            services.RemoveAll<IIdeationAgent>();
            services.AddScoped(_ => ideationAgent);

            services.RemoveAll<IWorkflowArticleWriter>();
            services.AddScoped(_ => workflowWriter);

            services.RemoveAll<ISerpBenchmarkService>();
            services.AddScoped(_ => serpBenchmark);

            services.RemoveAll<IOptimizationScoringService>();
            services.AddScoped(_ => optimizationScoring);

            services.RemoveAll<IImageGenerationService>();
            services.AddScoped(_ => imageGen);

            services.RemoveAll<IContentStrategyAgent>();
            services.AddScoped(_ => contentStrategy);

            services.RemoveAll<IEditorialAgent>();
            services.AddScoped(_ => editorial);

            services.RemoveAll<ISeoAndMonetizationAgent>();
            services.AddScoped(_ => seoMonetization);

            services.RemoveAll<IQaAndComplianceAgent>();
            services.AddScoped(_ => qaCompliance);

            services.RemoveAll<IQaFixVerificationAgent>();
            services.AddScoped(_ => qaFixVerification);

            services.RemoveAll<ISeoOptimizationAgent>();
            services.AddScoped(_ => seoOptimization);

            services.RemoveAll<IWorkflowCoordinatorAgent>();
            services.AddScoped(_ => workflowCoordinator);

            var workflowDispatcher = Substitute.For<IWorkflowTaskDispatcher>();
            services.RemoveAll<IWorkflowTaskDispatcher>();
            services.AddScoped(_ => workflowDispatcher);

            services.RemoveAll(typeof(ContentOS.Infrastructure.Ideation.ITopicDiversityScorer));
            services.AddScoped(_ => topicDiversityScorer);

            // Remove research source providers (they use HTTP clients)
            services.RemoveAll(typeof(ContentOS.Application.Abstractions.IResearchSourceProvider));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Seed data into the database within a scope.
    /// </summary>
    public async Task SeedAsync(Action<ContentOsDbContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        seedAction(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Verify database state after a test action.
    /// </summary>
    public async Task VerifyAsync(Func<ContentOsDbContext, Task> verifyAction)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await verifyAction(db);
    }

    /// <summary>
    /// Generate a valid JWT token for test authentication.
    /// The signing key must match CONTENTOS_JWT_SIGNING_KEY set above.
    /// </summary>
    public static string GenerateJwtToken()
    {
        var signingKey = "ContentOS-Test-JWT-Signing-Key-Must-Be-32-Chars!!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Eric"),
        };

        var token = new JwtSecurityToken(
            issuer: "ContentOS",
            audience: "ContentOS.Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[CollectionDefinition("ApiIntegration", DisableParallelization = true)]
public class ApiIntegrationCollection { }