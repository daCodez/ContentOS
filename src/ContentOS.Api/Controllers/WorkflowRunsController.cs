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
[Route("api/v{version:apiVersion}/workflow-runs")]
[Produces("application/json")]
[Consumes("application/json")]
public class WorkflowRunsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowRunsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("article")]
    public async Task<ActionResult<ApiResult<IEnumerable<WorkflowDefinitionRunDto>>>> GetArticleRuns(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetArticleRunsQuery(), cancellationToken);
        return Ok(ApiResult<IEnumerable<WorkflowDefinitionRunDto>>.Successful(result));
    }

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<ApiResult<WorkflowDefinitionRunDto>>> GetRun(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWorkflowDefinitionRunQuery(runId), cancellationToken);
        if (result == null) return NotFound(ApiResult<WorkflowDefinitionRunDto>.Failed("Workflow run not found"));
        return Ok(ApiResult<WorkflowDefinitionRunDto>.Successful(result));
    }

    [HttpPost("{runId:guid}/actions/{actionId:guid}/retry")]
    public async Task<ActionResult<ApiResult<bool>>> RetryAction(Guid runId, Guid actionId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RetryWorkflowActionRunCommand(runId, actionId, "User"), cancellationToken);
        return Ok(ApiResult<bool>.Successful(result));
    }
}