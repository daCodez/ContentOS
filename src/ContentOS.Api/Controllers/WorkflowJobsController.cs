using ContentOS.Application.Commands;
using ContentOS.Application.Queries;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workflow-jobs")]
[Produces("application/json")]
[Consumes("application/json")]
public class WorkflowJobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowJobsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWorkflowJobByIdQuery(id), cancellationToken);
        if (result is null) return NotFound(ApiResult<object>.Failed("Workflow job not found."));
        return Ok(ApiResult<object>.Successful(result));
    }

    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWorkflowTasksByJobQuery(id), cancellationToken);
        return Ok(ApiResult<object>.Successful(result));
    }

    [HttpPost("{id:guid}/rerun-from/{displayOrder:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RerunFrom(Guid id, int displayOrder, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new RerunWorkflowFromTaskCommand(id, displayOrder, "Eric"), cancellationToken);
        return success
            ? Ok(ApiResult<object>.Successful(new { workflowJobId = id, startingDisplayOrder = displayOrder }, "Workflow rerun queued."))
            : NotFound(ApiResult<object>.Failed("Workflow job or starting task not found."));
    }
}
