using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetSitesQueryHandler : IRequestHandler<GetSitesQuery, IEnumerable<Site>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetSitesQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Site>> Handle(GetSitesQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Sites
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}
