using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Application.Handlers;
using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ContentOS.Application.Tests;

public class CreateArticleCommandHandlerTests
{
    private readonly IArticleRepository _articleRepository;
    private readonly CreateArticleCommandHandler _handler;

    public CreateArticleCommandHandlerTests()
    {
        _articleRepository = Substitute.For<IArticleRepository>();
        _handler = new CreateArticleCommandHandler(_articleRepository);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesArticleAndReturnsId()
    {
        var command = new CreateArticleCommand("Test Article", "Content here", "Summary", "Author");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        await _articleRepository.Received(1).AddAsync(
            Arg.Is<Article>(a =>
                a.Title == "Test Article" &&
                a.Content == "Content here" &&
                a.Summary == "Summary" &&
                a.Author == "Author" &&
                a.IsPublished == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DefaultsToUnpublished()
    {
        var command = new CreateArticleCommand("Title", "Content", "Summary", "Author");

        await _handler.Handle(command, CancellationToken.None);

        await _articleRepository.Received(1).AddAsync(
            Arg.Is<Article>(a => !a.IsPublished),
            Arg.Any<CancellationToken>());
    }
}

public class GetArticleByIdQueryHandlerTests
{
    private readonly IArticleRepository _articleRepository;
    private readonly GetArticleByIdQueryHandler _handler;

    public GetArticleByIdQueryHandlerTests()
    {
        _articleRepository = Substitute.For<IArticleRepository>();
        _handler = new GetArticleByIdQueryHandler(_articleRepository);
    }

    [Fact]
    public async Task Handle_ArticleExists_ReturnsDto()
    {
        var articleId = Guid.NewGuid();
        var article = new Article
        {
            Id = articleId, Title = "Test", Content = "Content",
            Summary = "Summary", Author = "Author", IsPublished = true, Status = "Draft"
        };
        _articleRepository.GetByIdAsync(articleId, Arg.Any<CancellationToken>())
            .Returns(article);

        var result = await _handler.Handle(new GetArticleByIdQuery(articleId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(articleId);
        result.Title.Should().Be("Test");
    }

    [Fact]
    public async Task Handle_ArticleNotFound_ReturnsNull()
    {
        _articleRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Article?)null);

        var result = await _handler.Handle(new GetArticleByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}

public class GetArticlesQueryHandlerTests
{
    private readonly IArticleRepository _articleRepository;
    private readonly GetArticlesQueryHandler _handler;

    public GetArticlesQueryHandlerTests()
    {
        _articleRepository = Substitute.For<IArticleRepository>();
        _handler = new GetArticlesQueryHandler(_articleRepository);
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedResult()
    {
        var articles = new List<Article>
        {
            new() { Id = Guid.NewGuid(), Title = "Article 1" },
            new() { Id = Guid.NewGuid(), Title = "Article 2" }
        };
        _articleRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(articles);

        var result = await _handler.Handle(new GetArticlesQuery(Page: 1, PageSize: 10), CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_NoArticles_ReturnsEmptyPaginatedResult()
    {
        _articleRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Article>());

        var result = await _handler.Handle(new GetArticlesQuery(Page: 1, PageSize: 10), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}