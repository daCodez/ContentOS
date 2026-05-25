using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class QueryHandlerTests
{
    private ContentOsDbContext _dbContext = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<(Guid siteId, Guid ideaId, Guid templateId, Guid jobId)> SeedFullWorkflowAsync()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test Site", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test Topic", PrimaryKeyword = "test", Status = "Approved" };
        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "QueryTemplate", IsActive = true };
        var taskTemplate = new WorkflowTaskTemplate
        {
            Id = Guid.NewGuid(), WorkflowTemplateId = template.Id, Name = "Research",
            StageName = "Research", DisplayOrder = 1, AssignedAgent = "ResearchAgent"
        };
        var job = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id,
            Status = "Queued", CurrentStage = "Research"
        };
        var task = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(), ContentWorkflowJobId = job.Id, WorkflowTaskTemplateId = taskTemplate.Id,
            Name = "Research", StageName = "Research", DisplayOrder = 1, AssignedAgent = "ResearchAgent", Status = ContentOS.Domain.Enums.TaskStatus.Ready
        };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.WorkflowTaskTemplates.Add(taskTemplate);
        _dbContext.ContentWorkflowJobs.Add(job);
        _dbContext.ContentWorkflowTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        return (site.Id, idea.Id, template.Id, job.Id);
    }

    // === GetContentIdeasQueryHandler ===

    [Test]
    public async Task GetContentIdeas_ReturnsNonArchivedIdeas()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea1 = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Active", PrimaryKeyword = "active", Status = "NeedsReview" };
        var idea2 = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Archived", PrimaryKeyword = "archived", Status = "Archived" };
        var idea3 = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Approved", PrimaryKeyword = "approved", Status = "Approved" };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.AddRange(idea1, idea2, idea3);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentIdeasQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentIdeasQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Id == idea1.Id);
        result.Should().Contain(x => x.Id == idea3.Id);
        result.Should().NotContain(x => x.Id == idea2.Id);
    }

    [Test]
    public async Task GetContentIdeas_OrderedByCreatedUtcDescending()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea1 = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Older", PrimaryKeyword = "older", Status = "NeedsReview", CreatedUtc = DateTime.UtcNow.AddDays(-1) };
        var idea2 = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Newer", PrimaryKeyword = "newer", Status = "NeedsReview", CreatedUtc = DateTime.UtcNow };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.AddRange(idea1, idea2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentIdeasQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentIdeasQuery(), CancellationToken.None);

        result.First().Id.Should().Be(idea2.Id);
    }

    // === GetContentIdeaByIdQueryHandler ===

    [Test]
    public async Task GetContentIdeaById_ReturnsIdea_WhenExists()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Find Me", PrimaryKeyword = "find", Status = "NeedsReview" };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentIdeaByIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentIdeaByIdQuery(idea.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Find Me");
    }

    [Test]
    public async Task GetContentIdeaById_ReturnsNull_WhenNotExists()
    {
        var handler = new GetContentIdeaByIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentIdeaByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    // === GetWorkflowJobByContentIdeaIdQueryHandler ===

    [Test]
    public async Task GetWorkflowJobByContentIdeaId_ReturnsJob_WhenExists()
    {
        var data = await SeedFullWorkflowAsync();

        var handler = new GetWorkflowJobByContentIdeaIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowJobByContentIdeaIdQuery(data.ideaId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ContentIdeaId.Should().Be(data.ideaId);
    }

    [Test]
    public async Task GetWorkflowJobByContentIdeaId_ReturnsNull_WhenNotExists()
    {
        var handler = new GetWorkflowJobByContentIdeaIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowJobByContentIdeaIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    // === GetWorkflowJobByIdQueryHandler ===

    [Test]
    public async Task GetWorkflowJobById_ReturnsJob_WhenExists()
    {
        var data = await SeedFullWorkflowAsync();

        var handler = new GetWorkflowJobByIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowJobByIdQuery(data.jobId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(data.jobId);
    }

    [Test]
    public async Task GetWorkflowJobById_ReturnsNull_WhenNotExists()
    {
        var handler = new GetWorkflowJobByIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowJobByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    // === GetWorkflowTasksByJobQueryHandler ===

    [Test]
    public async Task GetWorkflowTasksByJob_ReturnsTasksOrderedByDisplayOrder()
    {
        var data = await SeedFullWorkflowAsync();

        var handler = new GetWorkflowTasksByJobQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowTasksByJobQuery(data.jobId), CancellationToken.None);

        result.Should().NotBeEmpty();
        result.Should().ContainSingle(x => x.ContentWorkflowJobId == data.jobId);
    }

    [Test]
    public async Task GetWorkflowTasksByJob_ReturnsEmpty_WhenNoTasks()
    {
        var handler = new GetWorkflowTasksByJobQueryHandler(_dbContext);
        var result = await handler.Handle(new GetWorkflowTasksByJobQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // === GetSitesQueryHandler ===

    [Test]
    public async Task GetSites_ReturnsOnlyActiveSites()
    {
        var activeSite = new Site { Id = Guid.NewGuid(), Name = "Active Site", Domain = "active.com", IsActive = true };
        var inactiveSite = new Site { Id = Guid.NewGuid(), Name = "Inactive Site", Domain = "inactive.com", IsActive = false };

        _dbContext.Sites.AddRange(activeSite, inactiveSite);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSitesQueryHandler(_dbContext);
        var result = await handler.Handle(new GetSitesQuery(), CancellationToken.None);

        result.Should().ContainSingle(x => x.Id == activeSite.Id);
        result.Should().NotContain(x => x.Id == inactiveSite.Id);
    }

    // === GetContentArtifactsByJobQueryHandler ===

    [Test]
    public async Task GetContentArtifactsByJob_ReturnsArtifactsOrderedByCreatedUtc()
    {
        var data = await SeedFullWorkflowAsync();

        var artifact1 = new ContentArtifact
        {
            Id = Guid.NewGuid(), ContentWorkflowJobId = data.jobId,
            ArtifactType = "DraftArticle", Title = "Draft",
            ContentJson = "{}", CreatedUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var artifact2 = new ContentArtifact
        {
            Id = Guid.NewGuid(), ContentWorkflowJobId = data.jobId,
            ArtifactType = "QaReport", Title = "QA",
            ContentJson = "{}", CreatedUtc = DateTime.UtcNow
        };
        _dbContext.ContentArtifacts.AddRange(artifact1, artifact2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentArtifactsByJobQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentArtifactsByJobQuery(data.jobId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.First().Id.Should().Be(artifact1.Id);
    }

    // === GetContentResearchSourcesByIdeaIdQueryHandler ===

    [Test]
    public async Task GetContentResearchSources_ReturnsSourcesForIdea()
    {
        var data = await SeedFullWorkflowAsync();

        var source = new ContentResearchSource
        {
            Id = Guid.NewGuid(), ContentIdeaId = data.ideaId,
            SourceType = "Web", SourceTitle = "Test Source",
            SourceUrl = "https://example.com"
        };
        _dbContext.ContentResearchSources.Add(source);
        await _dbContext.SaveChangesAsync();

        var handler = new GetContentResearchSourcesByIdeaIdQueryHandler(_dbContext);
        var result = await handler.Handle(new GetContentResearchSourcesByIdeaIdQuery(data.ideaId), CancellationToken.None);

        result.Should().ContainSingle(x => x.Id == source.Id);
    }
}