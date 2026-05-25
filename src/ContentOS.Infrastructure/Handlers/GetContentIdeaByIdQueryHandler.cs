using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class GetContentIdeaByIdQueryHandler : IRequestHandler<GetContentIdeaByIdQuery, ContentIdea?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetContentIdeaByIdQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ContentIdea?> Handle(GetContentIdeaByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ContentIdeas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}