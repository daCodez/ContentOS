using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Infrastructure.Handlers;

public class CreateSiteCommandHandler : IRequestHandler<CreateSiteCommand, Site>
{
    private readonly ContentOsDbContext _dbContext;

    public CreateSiteCommandHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Site> Handle(CreateSiteCommand request, CancellationToken cancellationToken)
    {
        var site = new Site
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Domain = request.Domain.Trim().ToLowerInvariant(),
            Niche = request.Niche.Trim(),
            DefaultTone = request.DefaultTone.Trim(),
            PlatformType = string.IsNullOrWhiteSpace(request.PlatformType) ? "WordPress" : request.PlatformType.Trim(),
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.Sites.Add(site);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return site;
    }
}