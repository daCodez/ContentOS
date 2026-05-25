using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Application.Handlers;
using ContentOS.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ContentOS.Application.Tests;

public class LoginCommandHandlerTests
{
    private readonly LoginCommandHandler _handler = new();

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResponse()
    {
        var command = new LoginCommand("admin", "password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Token.Should().StartWith("mock-jwt-token-for-system-verification-");
        result.ExpiresIn.Should().Be(3600);
        result.Username.Should().Be("admin");
    }

    [Fact]
    public async Task Handle_InvalidCredentials_ThrowsException()
    {
        var command = new LoginCommand("admin", "wrong-password");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Handle_InvalidUsername_ThrowsException()
    {
        var command = new LoginCommand("notadmin", "password");

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Handle_GeneratesUniqueTokens()
    {
        var command = new LoginCommand("admin", "password");

        var result1 = await _handler.Handle(command, CancellationToken.None);
        var result2 = await _handler.Handle(command, CancellationToken.None);

        result1.Token.Should().NotBe(result2.Token);
    }
}

public class RefreshTokenCommandHandlerTests
{
    private readonly RefreshTokenCommandHandler _handler = new();

    [Fact]
    public async Task Handle_ReturnsNewToken()
    {
        var command = new RefreshTokenCommand("admin");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Token.Should().StartWith("mock-refreshed-jwt-token-");
        result.ExpiresIn.Should().Be(3600);
        result.Username.Should().Be("admin");
    }

    [Fact]
    public async Task Handle_GeneratesUniqueTokens()
    {
        var command = new RefreshTokenCommand("admin");

        var result1 = await _handler.Handle(command, CancellationToken.None);
        var result2 = await _handler.Handle(command, CancellationToken.None);

        result1.Token.Should().NotBe(result2.Token);
    }
}