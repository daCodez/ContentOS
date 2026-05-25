using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetWorkflowJobListQueryHandler : IRequestHandler<GetWorkflowJobListQuery, IEnumerable<WorkflowJobListItemDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowJobListQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<WorkflowJobListItemDto>> Handle(GetWorkflowJobListQuery request, CancellationToken cancellationToken)
    {
        return await (from job in _dbContext.ContentWorkflowJobs
                      join idea in _dbContext.ContentIdeas on job.ContentIdeaId equals idea.Id
                      join site in _dbContext.Sites on idea.SiteId equals site.Id
                      orderby job.LastUpdatedUtc descending
                      select new WorkflowJobListItemDto
                      {
                          WorkflowJobId = job.Id,
                          ContentIdeaId = idea.Id,
                          Title = idea.Title,
                          SiteName = site.Name,
                          ContentType = idea.ContentType,
                          Status = job.Status,
                          CurrentStage = job.CurrentStage,
                          LastUpdatedUtc = job.LastUpdatedUtc
                      }).ToListAsync(cancellationToken);
    }
}
