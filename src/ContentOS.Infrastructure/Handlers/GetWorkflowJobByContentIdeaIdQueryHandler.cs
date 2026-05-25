using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetWorkflowJobByContentIdeaIdQueryHandler : IRequestHandler<GetWorkflowJobByContentIdeaIdQuery, ContentWorkflowJob?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowJobByContentIdeaIdQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ContentWorkflowJob?> Handle(GetWorkflowJobByContentIdeaIdQuery request, CancellationToken cancellationToken)
    {
        return _dbContext.ContentWorkflowJobs
            .FirstOrDefaultAsync(x => x.ContentIdeaId == request.ContentIdeaId, cancellationToken);
    }
}
