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
[Route("api/v{version:apiVersion}/versions")]
[Produces("application/json")]
[Consumes("application/json")]
public class VersionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VersionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{articleId}")]
    public async Task<ActionResult<ApiResult<IEnumerable<ArticleVersionDto>>>> GetVersions(Guid articleId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetArticleVersionsQuery(articleId), cancellationToken);
        return Ok(ApiResult<IEnumerable<ArticleVersionDto>>.Successful(result));
    }

    [HttpPost("restore")]
    public async Task<ActionResult<EmptyApiResult>> RestoreVersion([FromBody] RestoreArticleVersionCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Version restored successfully"));
    }

    [HttpDelete("{versionId}")]
    public async Task<ActionResult<EmptyApiResult>> DeleteVersion(Guid versionId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteArticleVersionCommand(versionId), cancellationToken);
        return Ok(EmptyApiResult.Successful("Version deleted successfully"));
    }
}
