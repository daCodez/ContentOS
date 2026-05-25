using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class CreateSiteCommandHandlerTests
{
    private ContentOsDbContext _dbContext = null!;
    private CreateSiteCommandHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        _handler = new CreateSiteCommandHandler(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Handle_ValidCommand_CreatesSite()
    {
        var command = new CreateSiteCommand
        {
            Name = "My Blog",
            Domain = "myblog.com",
            Niche = "Personal Finance",
            DefaultTone = "Friendly"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("My Blog");
        result.Domain.Should().Be("myblog.com");
        result.Niche.Should().Be("Personal Finance");
        result.DefaultTone.Should().Be("Friendly");
        result.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task Handle_DomainIsLowercased()
    {
        var command = new CreateSiteCommand
        {
            Name = "Test",
            Domain = "MyBlog.COM",
            Niche = "Test"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Domain.Should().Be("myblog.com");
    }

    [Test]
    public async Task Handle_DefaultPlatformTypeIsWordPress()
    {
        var command = new CreateSiteCommand
        {
            Name = "Test",
            Domain = "test.com",
            Niche = "Test"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.PlatformType.Should().Be("WordPress");
    }

    [Test]
    public async Task Handle_SetsTimestamps()
    {
        var command = new CreateSiteCommand
        {
            Name = "Test",
            Domain = "test.com",
            Niche = "Test"
        };

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(command, CancellationToken.None);

        result.CreatedUtc.Should().BeOnOrAfter(before - TimeSpan.FromSeconds(1));
        result.UpdatedUtc.Should().BeOnOrAfter(before - TimeSpan.FromSeconds(1));
    }
}