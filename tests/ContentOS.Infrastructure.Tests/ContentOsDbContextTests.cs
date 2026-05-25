using ContentOS.Domain.Entities;
using ContentOS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class ContentOsDbContextTests
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

    [Test]
    public async Task ContentIdeas_CanBeCreatedAndQueried()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test Site", Domain = "test.com" };
        _dbContext.Sites.Add(site);

        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Title = "Test Article",
            PrimaryKeyword = "test keyword",
            Status = "Discovered"
        };
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        var result = await _dbContext.ContentIdeas.FirstOrDefaultAsync(x => x.Id == idea.Id);
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Article");
        result.Status.Should().Be("Discovered");
    }

    [Test]
    public async Task WorkflowTemplate_HasUniqueNameIndex()
    {
        var template1 = new WorkflowTemplate
        {
            Id = Guid.NewGuid(), Name = "LongFormBlogArticle", Description = "First"
        };
        _dbContext.WorkflowTemplates.Add(template1);
        await _dbContext.SaveChangesAsync();

        var template2 = new WorkflowTemplate
        {
            Id = Guid.NewGuid(), Name = "LongFormBlogArticle", Description = "Duplicate"
        };
        _dbContext.WorkflowTemplates.Add(template2);

        var act = () => _dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task ContentWorkflowJob_HasUniqueContentIdeaIdIndex()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        _dbContext.Sites.Add(site);

        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test" };
        _dbContext.ContentIdeas.Add(idea);

        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "Template" };
        _dbContext.WorkflowTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        var job1 = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued"
        };
        _dbContext.ContentWorkflowJobs.Add(job1);
        await _dbContext.SaveChangesAsync();

        var job2 = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued"
        };
        _dbContext.ContentWorkflowJobs.Add(job2);

        var act = () => _dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task ContentWorkflowTask_CanBeCreatedWithJob()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        _dbContext.Sites.Add(site);

        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test" };
        _dbContext.ContentIdeas.Add(idea);

        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "Template2" };
        _dbContext.WorkflowTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        var job = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued"
        };
        _dbContext.ContentWorkflowJobs.Add(job);

        var task = new ContentWorkflowTask
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            WorkflowTaskTemplateId = Guid.NewGuid(),
            Name = "Research",
            StageName = "Research",
            DisplayOrder = 1,
            AssignedAgent = "ResearchAgent",
            Status = ContentOS.Domain.Enums.TaskStatus.Ready
        };
        _dbContext.ContentWorkflowTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var savedTask = await _dbContext.ContentWorkflowTasks.FirstOrDefaultAsync(x => x.Id == task.Id);
        savedTask.Should().NotBeNull();
        savedTask!.Name.Should().Be("Research");
        savedTask.Status.Should().Be(ContentOS.Domain.Enums.TaskStatus.Ready);
    }

    [Test]
    public async Task ContentArtifacts_CanBeCreatedAndQueried()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        _dbContext.Sites.Add(site);

        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test" };
        _dbContext.ContentIdeas.Add(idea);

        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "ArtifactTemplate" };
        _dbContext.WorkflowTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        var job = new ContentWorkflowJob
        {
            Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued"
        };
        _dbContext.ContentWorkflowJobs.Add(job);

        var artifact = new ContentArtifact
        {
            Id = Guid.NewGuid(),
            ContentWorkflowJobId = job.Id,
            ArtifactType = "DraftArticle",
            Title = "Draft",
            ContentJson = "{\"title\": \"Test\"}",
            ContentText = "Test content"
        };
        _dbContext.ContentArtifacts.Add(artifact);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.ContentArtifacts.FirstOrDefaultAsync(x => x.Id == artifact.Id);
        saved.Should().NotBeNull();
        saved!.ArtifactType.Should().Be("DraftArticle");
    }

    [Test]
    public async Task Sites_CanBeCreatedAndQueried()
    {
        var site = new Site
        {
            Id = Guid.NewGuid(), Name = "My Blog", Domain = "myblog.com",
            Niche = "Personal Finance", IsActive = true
        };
        _dbContext.Sites.Add(site);
        await _dbContext.SaveChangesAsync();

        var result = await _dbContext.Sites.FirstOrDefaultAsync(x => x.Id == site.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("My Blog");
        result.IsActive.Should().BeTrue();
    }
}