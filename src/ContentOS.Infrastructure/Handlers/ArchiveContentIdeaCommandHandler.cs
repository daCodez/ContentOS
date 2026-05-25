using ContentOS.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Handlers;

public class ArchiveContentIdeaCommandHandler : IRequestHandler<ArchiveContentIdeaCommand>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<ArchiveContentIdeaCommandHandler> _logger;

    public ArchiveContentIdeaCommandHandler(ContentOsDbContext dbContext, ILogger<ArchiveContentIdeaCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(ArchiveContentIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _dbContext.ContentIdeas.FirstOrDefaultAsync(x => x.Id == request.ContentIdeaId, cancellationToken)
            ?? throw new InvalidOperationException("Content idea not found.");

        if (string.Equals(idea.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var hasWorkflow = await _dbContext.ContentWorkflowJobs.AnyAsync(x => x.ContentIdeaId == idea.Id, cancellationToken);
            if (hasWorkflow)
            {
                throw new InvalidOperationException("Approved ideas with workflows cannot be archived.");
            }
        }

        idea.Status = "Archived";
        idea.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Archived content idea {ContentIdeaId}", idea.Id);
    }
}
