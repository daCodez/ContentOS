using ContentOS.Application.Queries;
using MediatR;

namespace ContentOS.Web.Diagnostics;

public static class ArtifactDump
{
    public static async Task<IResult> GetWorkflowArtifacts(Guid id, IMediator mediator, CancellationToken ct)
    {
        var artifacts = await mediator.Send(new GetContentArtifactsByJobQuery(id), ct);
        return Results.Ok(new { isSuccessful = true, data = artifacts });
    }
}
