using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/publish")]
[Produces("application/json")]
[Consumes("application/json")]
public class PublishController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublishController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // POST: api/v1/publish/{articleId}
    [HttpPost("{articleId}")]
    public async Task<ActionResult<EmptyApiResult>> PublishArticle(Guid articleId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(EmptyApiResult.Failed("Validation failed"));
        }

        var command = new PublishArticleCommand(articleId);
        await _mediator.Send(command, cancellationToken);
        
        return Ok(EmptyApiResult.Successful("Article published successfully"));
    }

    // POST: api/v1/publish/schedule
    [HttpPost("schedule")]
    public async Task<ActionResult<EmptyApiResult>> ScheduleArticle([FromBody] ScheduleArticleCommand command, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(EmptyApiResult.Failed("Validation failed"));
        }

        await _mediator.Send(command, cancellationToken);
        
        return Ok(EmptyApiResult.Successful("Article scheduled successfully"));
    }
}