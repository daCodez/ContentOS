using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, ContentOS.Application.DTOs.AuthResponseDto>
{
    public Task<ContentOS.Application.DTOs.AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // TEMPORARY: Mock authentication for system verification
        // Replace with real authentication against user store/database
        
        if (request.Username != "admin" || request.Password != "password")
        {
            // In a real implementation, this would return an error via the response wrapper
            // For now, we'll throw an exception that gets handled by the controller
            throw new System.Exception("Invalid credentials");
        }

        var response = new ContentOS.Application.DTOs.AuthResponseDto
        {
            Token = "mock-jwt-token-for-system-verification-" + Guid.NewGuid().ToString(),
            ExpiresIn = 3600,
            Username = request.Username
        };

        return Task.FromResult(response);
    }
}