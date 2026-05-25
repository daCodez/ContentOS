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
[Route("api/v{version:apiVersion}/articles")]
[Produces("application/json")]
[Consumes("application/json")]
public class ArticlesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ArticlesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ArticleDto>>>> GetArticles(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetArticlesQuery(), cancellationToken);
        return Ok(ApiResult<IEnumerable<ArticleDto>>.Successful(result.Items));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<ArticleDto>>> GetArticleById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetArticleByIdQuery(id), cancellationToken);
        if (result == null) return UnprocessableEntity(ApiResult<ArticleDto>.Failed("Article not found"));
        return Ok(ApiResult<ArticleDto>.Successful(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<Guid>>> CreateArticle([FromBody] CreateArticleCommand command, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ApiResult<Guid>.Failed("Validation failed"));
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetArticleById), new { id = result }, ApiResult<Guid>.Successful(result, "Article created successfully"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmptyApiResult>> UpdateArticle(Guid id, [FromBody] UpdateArticleCommand command, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(EmptyApiResult.Failed("Validation failed"));
        if (id != command.Id) return BadRequest(EmptyApiResult.Failed("Route ID does not match command ID"));
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Article updated successfully"));
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<EmptyApiResult>> UpdateArticleStatus(Guid id, [FromBody] UpdateArticleStatusCommand command, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(EmptyApiResult.Failed("Validation failed"));
        if (id != command.Id) return BadRequest(EmptyApiResult.Failed("Route ID does not match command ID"));
        await _mediator.Send(command, cancellationToken);
        return Ok(EmptyApiResult.Successful("Article status updated successfully"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<EmptyApiResult>> DeleteArticle(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteArticleCommand(id), cancellationToken);
        return Ok(EmptyApiResult.Successful("Article deleted successfully"));
    }

    /// <summary>
    /// Assembles a ready-to-render article from a completed workflow job.
    /// Finds the key task outputs (WriterAgent, HumanizerAgent, QaScoringAgent)
    /// and stitches them into a single clean DTO — no frontend parsing required.
    /// </summary>
    [HttpGet("assembly/{workflowJobId:guid}")]
    public async Task<ActionResult<ApiResult<ArticleAssemblyDto>>> GetArticleAssembly(Guid workflowJobId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetArticleAssemblyByWorkflowJobIdQuery(workflowJobId), cancellationToken);
        if (result == null) return NotFound(ApiResult<ArticleAssemblyDto>.Failed("Article assembly not found for this workflow job."));
        return Ok(ApiResult<ArticleAssemblyDto>.Successful(result));
    }
}
