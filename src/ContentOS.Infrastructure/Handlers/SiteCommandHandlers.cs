using ContentOS.Application.Commands;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class UpdateSiteCommandHandler : IRequestHandler<UpdateSiteCommand, Site>
{
    private readonly ContentOsDbContext _dbContext;

    public UpdateSiteCommandHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Site> Handle(UpdateSiteCommand request, CancellationToken cancellationToken)
    {
        var site = await _dbContext.Sites.FindAsync([request.Id], cancellationToken);
        if (site is null)
            throw new KeyNotFoundException($"Site not found: {request.Id}");

        site.Name = request.Name;
        site.Domain = request.Domain;
        site.Niche = request.Niche;
        site.DefaultTone = request.DefaultTone;
        site.PlatformType = request.PlatformType;
        site.IsActive = request.IsActive;
        site.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return site;
    }
}

public class DeleteSiteCommandHandler : IRequestHandler<DeleteSiteCommand, bool>
{
    private readonly ContentOsDbContext _dbContext;

    public DeleteSiteCommandHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteSiteCommand request, CancellationToken cancellationToken)
    {
        var site = await _dbContext.Sites.FindAsync([request.Id], cancellationToken);
        if (site is null)
            return false;

        _dbContext.Sites.Remove(site);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}