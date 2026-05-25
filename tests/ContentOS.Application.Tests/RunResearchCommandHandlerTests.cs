using ContentOS.Application.Abstractions;
using ContentOS.Application.Commands;
using ContentOS.Application.Handlers;
using ContentOS.Application.Research;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ContentOS.Application.Tests;

public class RunResearchCommandHandlerTests
{
    private readonly IResearchAgent _researchAgent;
    private readonly RunResearchCommandHandler _handler;

    public RunResearchCommandHandlerTests()
    {
        _researchAgent = Substitute.For<IResearchAgent>();
        _handler = new RunResearchCommandHandler(_researchAgent);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsResearchResult()
    {
        // Arrange
        var siteId = Guid.NewGuid();
        var expectedResult = new RunResearchResult
        {
            SiteId = siteId,
            RequestedIdeaCount = 5,
            FindingsGathered = 10,
            CandidatesCreated = 8,
            IdeasSaved = 5,
            DuplicatesSkipped = 3,
            SavedTitles = ["Topic A", "Topic B", "Topic C", "Topic D", "Topic E"]
        };

        _researchAgent.RunAsync(siteId, 5, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var command = new RunResearchCommand(siteId, 5);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
        result.IdeasSaved.Should().Be(5);
        result.DuplicatesSkipped.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithNullRequestedIdeaCount_DelegatesToAgent()
    {
        // Arrange
        var siteId = Guid.NewGuid();
        var expectedResult = new RunResearchResult { SiteId = siteId };

        _researchAgent.RunAsync(siteId, null, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var command = new RunResearchCommand(siteId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
        await _researchAgent.Received(1).RunAsync(siteId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToAgent()
    {
        // Arrange
        var siteId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _researchAgent.RunAsync(siteId, null, token)
            .Returns(new RunResearchResult { SiteId = siteId });

        var command = new RunResearchCommand(siteId);

        // Act
        await _handler.Handle(command, token);

        // Assert
        await _researchAgent.Received(1).RunAsync(siteId, null, token);
    }

    [Fact]
    public async Task Handle_WhenAgentThrows_PropagatesException()
    {
        // Arrange
        var siteId = Guid.NewGuid();
        _researchAgent.RunAsync(siteId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns<Task<RunResearchResult>>(_ => throw new HttpRequestException("Research service unavailable"));

        var command = new RunResearchCommand(siteId, 3);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Research service unavailable");
    }
}