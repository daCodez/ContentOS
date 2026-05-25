using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class ArchiveContentIdeaCommandHandlerTests
{
    private ContentOsDbContext _dbContext = null!;
    private ILogger<ArchiveContentIdeaCommandHandler> _logger = null!;
    private ArchiveContentIdeaCommandHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _logger = Substitute.For<ILogger<ArchiveContentIdeaCommandHandler>>();
        _handler = new ArchiveContentIdeaCommandHandler(_dbContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Handle_ValidIdea_SetsStatusToArchived()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test", Status = "NeedsReview" };
        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(new ArchiveContentIdeaCommand(idea.Id), CancellationToken.None);

        var archived = await _dbContext.ContentIdeas.FirstAsync(x => x.Id == idea.Id);
        archived.Status.Should().Be("Archived");
    }

    [Test]
    public async Task Handle_UpdatesTimestamp()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test", Status = "NeedsReview" };
        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        var before = DateTime.UtcNow;
        await _handler.Handle(new ArchiveContentIdeaCommand(idea.Id), CancellationToken.None);

        var archived = await _dbContext.ContentIdeas.FirstAsync(x => x.Id == idea.Id);
        archived.UpdatedUtc.Should().BeOnOrAfter(before - TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Handle_IdeaNotFound_ThrowsInvalidOperationException()
    {
        var randomId = Guid.NewGuid();

        var act = () => _handler.Handle(new ArchiveContentIdeaCommand(randomId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Content idea not found.");
    }

    [Test]
    public async Task Handle_ApprovedIdeaWithWorkflow_ThrowsInvalidOperationException()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test", Status = "Approved" };
        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "ArchiveTemplate" };
        var job = new ContentWorkflowJob { Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued" };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var act = () => _handler.Handle(new ArchiveContentIdeaCommand(idea.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Approved ideas with workflows cannot be archived.");
    }

    [Test]
    public async Task Handle_ApprovedIdeaWithoutWorkflow_ArchivesSuccessfully()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Test", PrimaryKeyword = "test", Status = "Approved" };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(new ArchiveContentIdeaCommand(idea.Id), CancellationToken.None);

        var archived = await _dbContext.ContentIdeas.FirstAsync(x => x.Id == idea.Id);
        archived.Status.Should().Be("Archived");
    }
}