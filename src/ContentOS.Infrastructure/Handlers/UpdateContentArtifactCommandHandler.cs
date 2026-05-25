using ContentOS.Application.Commands.Artifacts;
using MediatR;

namespace ContentOS.Infrastructure.Handlers;

public class UpdateContentArtifactCommandHandler : IRequestHandler<UpdateContentArtifactCommand, bool>
{
    private readonly ContentOsDbContext _dbContext;

    public UpdateContentArtifactCommandHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(UpdateContentArtifactCommand request, CancellationToken cancellationToken)
    {
        var artifact = await _dbContext.ContentArtifacts.FindAsync([request.ArtifactId], cancellationToken);
        if (artifact is null)
        {
            return false;
        }

        artifact.ContentJson = request.ContentJson;
        artifact.ContentText = request.ContentText;
        artifact.CreatedByAgent = request.UpdatedBy;
        artifact.VersionNumber += 1;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
