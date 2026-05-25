using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetContentArtifactsByJobQueryHandler : IRequestHandler<GetContentArtifactsByJobQuery, IEnumerable<ContentArtifact>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetContentArtifactsByJobQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ContentArtifact>> Handle(GetContentArtifactsByJobQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ContentArtifacts
            .Where(x => x.ContentWorkflowJobId == request.WorkflowJobId)
            .OrderBy(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }
}
