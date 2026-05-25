using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetContentIdeasQueryHandler : IRequestHandler<GetContentIdeasQuery, IEnumerable<ContentIdea>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetContentIdeasQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ContentIdea>> Handle(GetContentIdeasQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ContentIdeas
            .Where(x => x.Status != "Archived")
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }
}
