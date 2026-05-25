using ContentOS.Application.Commands;
using ContentOS.Application.Queries;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/content-ideas")]
[Produces("application/json")]
[Consumes("application/json")]
public class ContentIdeasController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContentIdeasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContentIdeasQuery(), cancellationToken);
        return Ok(ApiResult<object>.Successful(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContentIdeaCommand command, CancellationToken cancellationToken)
    {
        var contentIdeaId = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResult<object>.Successful(new { contentIdeaId }, "Content idea created."));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var workflowJobId = await _mediator.Send(new ApproveContentIdeaCommand(id, "Eric"), cancellationToken);
        return Ok(ApiResult<object>.Successful(new { workflowJobId }, "Content idea approved."));
    }

    [HttpPost("research/run/{siteId:guid}")]
    public async Task<IActionResult> RunResearch(Guid siteId, [FromBody] RunResearchRequest? request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RunResearchCommand(siteId, request?.RequestedIdeaCount), cancellationToken);
        return Ok(ApiResult<object>.Successful(result, "Research run completed."));
    }

    public sealed record RunResearchRequest(int? RequestedIdeaCount);

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ArchiveContentIdeaCommand(id), cancellationToken);
        return Ok(ApiResult<object>.Successful(new { contentIdeaId = id }, "Content idea archived."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteContentIdeaCommand(id), cancellationToken);
        return Ok(ApiResult<object>.Successful(new { contentIdeaId = id }, "Content idea deleted."));
    }
}
