using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class DeleteContentIdeaCommandHandlerTests
{
    private ContentOsDbContext _dbContext = null!;
    private ILogger<DeleteContentIdeaCommandHandler> _logger = null!;
    private DeleteContentIdeaCommandHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _logger = Substitute.For<ILogger<DeleteContentIdeaCommandHandler>>();
        _handler = new DeleteContentIdeaCommandHandler(_dbContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Handle_ValidIdea_DeletesFromDatabase()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Delete Me", PrimaryKeyword = "delete", Status = "NeedsReview" };
        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(new DeleteContentIdeaCommand(idea.Id), CancellationToken.None);

        var found = await _dbContext.ContentIdeas.FindAsync(idea.Id);
        found.Should().BeNull();
    }

    [Test]
    public async Task Handle_IdeaNotFound_ThrowsInvalidOperationException()
    {
        var randomId = Guid.NewGuid();

        var act = () => _handler.Handle(new DeleteContentIdeaCommand(randomId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Content idea not found.");
    }

    [Test]
    public async Task Handle_ApprovedIdea_ThrowsInvalidOperationException()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Approved Idea", PrimaryKeyword = "approved", Status = "Approved" };
        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        var act = () => _handler.Handle(new DeleteContentIdeaCommand(idea.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Approved ideas cannot be deleted. Archive them only if they have no workflow.");
    }

    [Test]
    public async Task Handle_IdeaWithWorkflow_ThrowsInvalidOperationException()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Has Workflow", PrimaryKeyword = "workflow", Status = "NeedsReview" };
        var template = new WorkflowTemplate { Id = Guid.NewGuid(), Name = "DeleteTemplate" };
        var job = new ContentWorkflowJob { Id = Guid.NewGuid(), ContentIdeaId = idea.Id, WorkflowTemplateId = template.Id, Status = "Queued" };

        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        _dbContext.WorkflowTemplates.Add(template);
        _dbContext.ContentWorkflowJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var act = () => _handler.Handle(new DeleteContentIdeaCommand(idea.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ideas with workflow jobs cannot be deleted.");
    }

    [Test]
    public async Task Handle_DiscoveredIdea_DeletesSuccessfully()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test", Domain = "test.com" };
        var idea = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Discovered", PrimaryKeyword = "discovered", Status = "Discovered" };
        _dbContext.Sites.Add(site);
        _dbContext.ContentIdeas.Add(idea);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(new DeleteContentIdeaCommand(idea.Id), CancellationToken.None);

        var found = await _dbContext.ContentIdeas.FindAsync(idea.Id);
        found.Should().BeNull();
    }
}