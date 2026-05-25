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
[Route("api/v{version:apiVersion}/queue")]
[Produces("application/json")]
[Consumes("application/json")]
public class QueueController : ControllerBase
{
    private readonly IMediator _mediator;

    public QueueController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<QueueItemDto>>>> GetQueue(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContentQueueQuery(), cancellationToken);
        return Ok(ApiResult<IEnumerable<QueueItemDto>>.Successful(result));
    }

    [HttpPost("process")]
    public async Task<ActionResult<EmptyApiResult>> ProcessQueue([FromBody] ProcessQueueCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Queue processed successfully"));
    }

    [HttpPost("fail")]
    public async Task<ActionResult<EmptyApiResult>> FailQueueItem([FromBody] FailQueueItemCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Queue item failed successfully"));
    }
}
