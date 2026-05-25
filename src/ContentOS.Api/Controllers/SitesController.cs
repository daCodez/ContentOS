using ContentOS.Application.Commands;
using ContentOS.Application.Queries;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sites")]
[Produces("application/json")]
public class SitesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SitesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSitesQuery(), cancellationToken);
        return Ok(ApiResult<object>.Successful(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Domain))
            return BadRequest(ApiResult<string>.Failed("Name and Domain are required."));

        var site = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { version = "1.0" }, ApiResult<object>.Successful(site));
    }
}