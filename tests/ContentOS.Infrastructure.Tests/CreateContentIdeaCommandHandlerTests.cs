using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class CreateContentIdeaCommandHandlerTests
{
    private ContentOsDbContext _dbContext = null!;
    private ILogger<CreateContentIdeaCommandHandler> _logger = null!;
    private CreateContentIdeaCommandHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _logger = Substitute.For<ILogger<CreateContentIdeaCommandHandler>>();
        _handler = new CreateContentIdeaCommandHandler(_dbContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<Guid> SeedSiteAsync()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Test Site", Domain = "test.com", Niche = "Testing", IsActive = true };
        _dbContext.Sites.Add(site);
        await _dbContext.SaveChangesAsync();
        return site.Id;
    }

    [Test]
    public async Task Handle_ValidCommand_CreatesIdea()
    {
        var siteId = await SeedSiteAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: siteId,
            Title: "Best Budgeting Apps",
            PrimaryKeyword: "best budgeting apps",
            Summary: "A practical guide",
            ContentType: "LongFormBlogArticle",
            SearchIntent: "informational"
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);

        var idea = await _dbContext.ContentIdeas.FindAsync(result);
        idea.Should().NotBeNull();
        idea!.Title.Should().Be("Best Budgeting Apps");
        idea.PrimaryKeyword.Should().Be("best budgeting apps");
        idea.Status.Should().Be("NeedsReview");
        idea.SiteId.Should().Be(siteId);
    }

    [Test]
    public async Task Handle_GeneratesSlugFromTitle()
    {
        var siteId = await SeedSiteAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: siteId,
            Title: "Best Budgeting Apps for Beginners",
            PrimaryKeyword: "best budgeting apps"
        );

        var result = await _handler.Handle(command, CancellationToken.None);
        var idea = await _dbContext.ContentIdeas.FindAsync(result);

        idea!.SlugSuggestion.Should().Be("best-budgeting-apps-for-beginners");
    }

    [Test]
    public async Task Handle_EmptySiteId_ThrowsArgumentException()
    {
        var command = new CreateContentIdeaCommand(
            SiteId: Guid.Empty,
            Title: "Test",
            PrimaryKeyword: "test"
        );

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*SiteId is required*");
    }

    [Test]
    public async Task Handle_EmptyTitle_ThrowsArgumentException()
    {
        var siteId = await SeedSiteAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: siteId,
            Title: "",
            PrimaryKeyword: "test"
        );

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Title is required*");
    }

    [Test]
    public async Task Handle_EmptyPrimaryKeyword_ThrowsArgumentException()
    {
        var siteId = await SeedSiteAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: siteId,
            Title: "Test Article",
            PrimaryKeyword: ""
        );

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PrimaryKeyword is required*");
    }

    [Test]
    public async Task Handle_InactiveSite_ThrowsInvalidOperationException()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Inactive", Domain = "inactive.com", IsActive = false };
        _dbContext.Sites.Add(site);
        await _dbContext.SaveChangesAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: site.Id,
            Title: "Test",
            PrimaryKeyword: "test"
        );

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist or is inactive*");
    }

    [Test]
    public async Task Handle_NonexistentSite_ThrowsInvalidOperationException()
    {
        var command = new CreateContentIdeaCommand(
            SiteId: Guid.NewGuid(),
            Title: "Test",
            PrimaryKeyword: "test"
        );

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist or is inactive*");
    }

    [Test]
    public async Task Handle_DefaultContentType_IsLongFormBlogArticle()
    {
        var siteId = await SeedSiteAsync();

        var command = new CreateContentIdeaCommand(
            SiteId: siteId,
            Title: "Test",
            PrimaryKeyword: "test"
        );

        var result = await _handler.Handle(command, CancellationToken.None);
        var idea = await _dbContext.ContentIdeas.FindAsync(result);

        idea!.ContentType.Should().Be("LongFormBlogArticle");
    }
}