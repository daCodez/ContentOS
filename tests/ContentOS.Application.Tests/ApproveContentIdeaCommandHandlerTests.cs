using ContentOS.Application.Abstractions;
using ContentOS.Application.Commands;
using ContentOS.Application.Handlers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ContentOS.Application.Tests;

public class ApproveContentIdeaCommandHandlerTests
{
    private readonly IWorkflowBootstrapService _workflowBootstrapService;
    private readonly ApproveContentIdeaCommandHandler _handler;

    public ApproveContentIdeaCommandHandlerTests()
    {
        _workflowBootstrapService = Substitute.For<IWorkflowBootstrapService>();
        _handler = new ApproveContentIdeaCommandHandler(_workflowBootstrapService);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsWorkflowJobId()
    {
        // Arrange
        var ideaId = Guid.NewGuid();
        var expectedJobId = Guid.NewGuid();
        _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>())
            .Returns(expectedJobId);

        var command = new ApproveContentIdeaCommand(ideaId, "Eric");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedJobId);
    }

    [Fact]
    public async Task Handle_DelegatesToWorkflowBootstrapService()
    {
        // Arrange
        var ideaId = Guid.NewGuid();
        var command = new ApproveContentIdeaCommand(ideaId, "Eric");

        _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _workflowBootstrapService.Received(1)
            .ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdeaNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var ideaId = Guid.NewGuid();
        _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("Content idea not found."));

        var command = new ApproveContentIdeaCommand(ideaId, "Eric");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Content idea not found.");
    }

    [Fact]
    public async Task Handle_WhenTemplateNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var ideaId = Guid.NewGuid();
        _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("Workflow template not found."));

        var command = new ApproveContentIdeaCommand(ideaId, "Eric");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Workflow template not found.");
    }

    [Fact]
    public async Task Handle_WhenWorkflowAlreadyExists_ReturnsExistingJobId()
    {
        // Arrange - this tests the idempotent behavior
        var ideaId = Guid.NewGuid();
        var existingJobId = Guid.NewGuid();
        _workflowBootstrapService.ApproveIdeaAndCreateWorkflowAsync(ideaId, "Eric", Arg.Any<CancellationToken>())
            .Returns(existingJobId);

        var command = new ApproveContentIdeaCommand(ideaId, "Eric");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(existingJobId);
    }
}