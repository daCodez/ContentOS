using ContentOS.Api.Controllers;
using ContentOS.Api.Responses;
using ContentOS.Application.Commands;
using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace ContentOS.Api.Tests;

public class ContentIdeasControllerTests
{
    private readonly IMediator _mediator;
    private readonly ContentIdeasController _controller;

    public ContentIdeasControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _controller = new ContentIdeasController(_mediator);
    }

    // === GetAll ===

    [Fact]
    public async Task GetAll_ReturnsOkWithIdeas()
    {
        var ideas = new List<ContentIdea>
        {
            new() { Id = Guid.NewGuid(), Title = "Test" }
        };
        _mediator.Send(Arg.Any<GetContentIdeasQuery>(), Arg.Any<CancellationToken>())
            .Returns(ideas);

        var result = await _controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
    }

    // === Create ===

    [Fact]
    public async Task Create_ValidCommand_ReturnsOkWithId()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<CreateContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(ideaId);

        var command = new CreateContentIdeaCommand(Guid.NewGuid(), "Test", "keyword");
        var result = await _controller.Create(command, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
        apiResult.Message.Should().Be("Content idea created.");
    }

    // === Approve ===

    [Fact]
    public async Task Approve_ValidId_ReturnsOkWithWorkflowJobId()
    {
        var ideaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        _mediator.Send(Arg.Any<ApproveContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(jobId);

        var result = await _controller.Approve(ideaId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
        apiResult.Message.Should().Be("Content idea approved.");
    }

    [Fact]
    public async Task Approve_SendsCommandWithHardcodedApprovedBy()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<ApproveContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await _controller.Approve(ideaId, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<ApproveContentIdeaCommand>(c => c.ApprovedBy == "Eric" && c.ContentIdeaId == ideaId),
            Arg.Any<CancellationToken>());
    }

    // === Archive ===

    [Fact]
    public async Task Archive_ValidId_ReturnsOk()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<ArchiveContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        var result = await _controller.Archive(ideaId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
        apiResult.Message.Should().Be("Content idea archived.");
    }

    [Fact]
    public async Task Archive_SendsCommandWithCorrectId()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<ArchiveContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        await _controller.Archive(ideaId, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<ArchiveContentIdeaCommand>(c => c.ContentIdeaId == ideaId),
            Arg.Any<CancellationToken>());
    }

    // === Delete ===

    [Fact]
    public async Task Delete_ValidId_ReturnsOk()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<DeleteContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        var result = await _controller.Delete(ideaId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
        apiResult.Message.Should().Be("Content idea deleted.");
    }

    [Fact]
    public async Task Delete_SendsCommandWithCorrectId()
    {
        var ideaId = Guid.NewGuid();
        _mediator.Send(Arg.Any<DeleteContentIdeaCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        await _controller.Delete(ideaId, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<DeleteContentIdeaCommand>(c => c.ContentIdeaId == ideaId),
            Arg.Any<CancellationToken>());
    }

    // === RunResearch ===

    [Fact]
    public async Task RunResearch_ValidRequest_ReturnsOk()
    {
        var siteId = Guid.NewGuid();
        var researchResult = new ContentOS.Application.Research.RunResearchResult
        {
            SiteId = siteId, IdeasSaved = 5
        };
        _mediator.Send(Arg.Any<RunResearchCommand>(), Arg.Any<CancellationToken>())
            .Returns(researchResult);

        var request = new ContentIdeasController.RunResearchRequest(5);
        var result = await _controller.RunResearch(siteId, request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResult = okResult.Value.Should().BeOfType<ApiResult<object>>().Subject;
        apiResult.IsSuccessful.Should().BeTrue();
        apiResult.Message.Should().Be("Research run completed.");
    }

    [Fact]
    public async Task RunResearch_NullRequest_PassesNullCountToCommand()
    {
        var siteId = Guid.NewGuid();
        _mediator.Send(Arg.Any<RunResearchCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ContentOS.Application.Research.RunResearchResult { SiteId = siteId });

        await _controller.RunResearch(siteId, null, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<RunResearchCommand>(c => c.SiteId == siteId && c.RequestedIdeaCount == null),
            Arg.Any<CancellationToken>());
    }
}