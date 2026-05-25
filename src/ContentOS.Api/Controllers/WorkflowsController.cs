using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workflows")]
[Produces("application/json")]
[Consumes("application/json")]
public class WorkflowsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<WorkflowDto>>>> GetWorkflows(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWorkflowsQuery(), cancellationToken);
        return Ok(ApiResult<IEnumerable<WorkflowDto>>.Successful(result.Items));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<WorkflowDto>>> GetWorkflowById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWorkflowByIdQuery(id), cancellationToken);
        if (result == null) return UnprocessableEntity(ApiResult<WorkflowDto>.Failed("Workflow not found"));
        return Ok(ApiResult<WorkflowDto>.Successful(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<WorkflowDto>>> CreateWorkflow([FromBody] CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetWorkflowById), new { id = result.Id }, ApiResult<WorkflowDto>.Successful(result, "Workflow created successfully"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmptyApiResult>> UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return BadRequest(EmptyApiResult.Failed("Route ID does not match command ID"));
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Workflow updated successfully"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<EmptyApiResult>> DeleteWorkflow(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteWorkflowCommand(id), cancellationToken);
        return Ok(EmptyApiResult.Successful("Workflow deleted successfully"));
    }
}
