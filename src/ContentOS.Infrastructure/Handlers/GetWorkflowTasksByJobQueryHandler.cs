using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetWorkflowTasksByJobQueryHandler : IRequestHandler<GetWorkflowTasksByJobQuery, IEnumerable<ContentWorkflowTask>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowTasksByJobQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ContentWorkflowTask>> Handle(GetWorkflowTasksByJobQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ContentWorkflowTasks
            .Where(x => x.ContentWorkflowJobId == request.WorkflowJobId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);
    }
}
