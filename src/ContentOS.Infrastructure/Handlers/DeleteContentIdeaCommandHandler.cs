using ContentOS.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Handlers;

public class DeleteContentIdeaCommandHandler : IRequestHandler<DeleteContentIdeaCommand>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<DeleteContentIdeaCommandHandler> _logger;

    public DeleteContentIdeaCommandHandler(ContentOsDbContext dbContext, ILogger<DeleteContentIdeaCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(DeleteContentIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _dbContext.ContentIdeas.FirstOrDefaultAsync(x => x.Id == request.ContentIdeaId, cancellationToken)
            ?? throw new InvalidOperationException("Content idea not found.");

        if (string.Equals(idea.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved ideas cannot be deleted. Archive them only if they have no workflow.");
        }

        var hasWorkflow = await _dbContext.ContentWorkflowJobs.AnyAsync(x => x.ContentIdeaId == idea.Id, cancellationToken);
        if (hasWorkflow)
        {
            throw new InvalidOperationException("Ideas with workflow jobs cannot be deleted.");
        }

        _dbContext.ContentIdeas.Remove(idea);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted content idea {ContentIdeaId}", idea.Id);
    }
}
