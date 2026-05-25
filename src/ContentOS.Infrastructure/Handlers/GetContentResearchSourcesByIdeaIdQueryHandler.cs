using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetContentResearchSourcesByIdeaIdQueryHandler : IRequestHandler<GetContentResearchSourcesByIdeaIdQuery, IEnumerable<ContentResearchSource>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetContentResearchSourcesByIdeaIdQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ContentResearchSource>> Handle(GetContentResearchSourcesByIdeaIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ContentResearchSources
            .Where(x => x.ContentIdeaId == request.ContentIdeaId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }
}
