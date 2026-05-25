using System;
using System.Collections.Generic;
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
[Route("api/v{version:apiVersion}/seo")]
[Produces("application/json")]
[Consumes("application/json")]
public class SeoController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("analysis/{articleId}")]
    public async Task<ActionResult<ApiResult<SeoAnalysisDto>>> GetSeoAnalysis(Guid articleId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSeoAnalysisQuery(articleId), cancellationToken);
        if (result == null) return UnprocessableEntity(ApiResult<SeoAnalysisDto>.Failed("SEO analysis not found"));
        return Ok(ApiResult<SeoAnalysisDto>.Successful(result));
    }

    [HttpGet("suggestions/{articleId}")]
    public async Task<ActionResult<ApiResult<SeoSuggestionDto>>> GetSeoOptimizationSuggestionsByArticle(Guid articleId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSeoOptimizationSuggestionsQuery(articleId), cancellationToken);
        if (result == null) return UnprocessableEntity(ApiResult<SeoSuggestionDto>.Failed("SEO suggestions not found"));
        return Ok(ApiResult<SeoSuggestionDto>.Successful(result));
    }
}
