using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Handlers;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, ContentOS.Application.DTOs.AuthResponseDto>
{
    public Task<ContentOS.Application.DTOs.AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // TEMPORARY: Mock token refresh for system verification
        // Replace with real refresh token validation and issuance
        
        var response = new ContentOS.Application.DTOs.AuthResponseDto
        {
            Token = "mock-refreshed-jwt-token-" + Guid.NewGuid().ToString(),
            ExpiresIn = 3600,
            Username = request.Username
        };

        return Task.FromResult(response);
    }
}