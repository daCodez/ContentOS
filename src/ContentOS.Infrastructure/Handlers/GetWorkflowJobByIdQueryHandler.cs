using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetWorkflowJobByIdQueryHandler : IRequestHandler<GetWorkflowJobByIdQuery, ContentWorkflowJob?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowJobByIdQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ContentWorkflowJob?> Handle(GetWorkflowJobByIdQuery request, CancellationToken cancellationToken)
    {
        return _dbContext.ContentWorkflowJobs.FirstOrDefaultAsync(x => x.Id == request.WorkflowJobId, cancellationToken);
    }
}
