using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Commands;

public record UpdateSiteCommand(
    Guid Id,
    string Name,
    string Domain,
    string Niche,
    string DefaultTone,
    string PlatformType,
    bool IsActive
) : IRequest<Site>;

public record DeleteSiteCommand(Guid Id) : IRequest<bool>;