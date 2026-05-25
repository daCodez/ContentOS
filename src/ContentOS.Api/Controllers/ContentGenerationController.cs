using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/content-generation")]
[Produces("application/json")]
[Consumes("application/json")]
public class ContentGenerationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWorkflowExportService _workflowExportService;
    private readonly IWorkflowMutationService _workflowMutationService;
    private readonly IWorkflowControlService _workflowControlService;

    public ContentGenerationController(IMediator mediator, IWorkflowExportService workflowExportService, IWorkflowMutationService workflowMutationService, IWorkflowControlService workflowControlService)
    {
        _mediator = mediator;
        _workflowExportService = workflowExportService;
        _workflowMutationService = workflowMutationService;
        _workflowControlService = workflowControlService;
    }

    [HttpPost("outline")]
    public async Task<ActionResult<ApiResult<ArticleOutlineDto>>> GenerateOutline([FromBody] GenerateArticleOutlineQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<ArticleOutlineDto>.Successful(result));
    }

    [HttpPost("titles")]
    public async Task<ActionResult<ApiResult<TitleIdeasDto>>> GenerateTitles([FromBody] GenerateTitleIdeasQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<TitleIdeasDto>.Successful(result));
    }

    [HttpPost("meta-tags")]
    public async Task<ActionResult<ApiResult<MetaTagsDto>>> GenerateMetaTags([FromBody] GenerateMetaTagsQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<MetaTagsDto>.Successful(result));
    }

    [HttpPost("faq-schema")]
    public async Task<ActionResult<ApiResult<FaqSchemaDto>>> GenerateFaqSchema([FromBody] GenerateFaqSchemaQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<FaqSchemaDto>.Successful(result));
    }

    [HttpPost("summary")]
    public async Task<ActionResult<ApiResult<SummaryDto>>> SummarizeArticle([FromBody] SummarizeArticleQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<SummaryDto>.Successful(result));
    }

    [HttpPost("rewrite")]
    public async Task<ActionResult<ApiResult<RewriteDto>>> RewriteArticle([FromBody] RewriteArticleByToneQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<RewriteDto>.Successful(result));
    }

    [HttpPost("humanize")]
    public async Task<ActionResult<ApiResult<HumanizedDto>>> HumanizeContent([FromBody] HumanizeContentQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResult<HumanizedDto>.Successful(result));
    }

    [HttpGet("workflows/{workflowJobId:guid}/export")]
    public async Task<ActionResult<ApiResult<WorkflowExportDto>>> ExportWorkflow(Guid workflowJobId, CancellationToken cancellationToken)
    {
        var result = await _workflowExportService.ExportAsync(workflowJobId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResult<WorkflowExportDto>.Failed("Workflow not found."));
        }

        return Ok(ApiResult<WorkflowExportDto>.Successful(result));
    }

    [HttpGet("workflows/{workflowJobId:guid}/export/markdown")]
    public async Task<IActionResult> ExportWorkflowMarkdown(Guid workflowJobId, CancellationToken cancellationToken)
    {
        var result = await _workflowExportService.ExportMarkdownAsync(workflowJobId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResult<string>.Failed("Workflow not found."));
        }

        return Content(result, "text/markdown");
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/manual")]
    public async Task<ActionResult<ApiResult<WorkflowTaskMutationResultDto>>> InjectManualTask(Guid workflowJobId, [FromBody] InjectWorkflowTaskRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowMutationService.InjectManualTaskAsync(workflowJobId, request, cancellationToken);
        return Ok(ApiResult<WorkflowTaskMutationResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/reopen")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> ReopenWorkflow(Guid workflowJobId, [FromBody] ReopenWorkflowRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowMutationService.ReopenWorkflowAsync(workflowJobId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/reorder")]
    public async Task<ActionResult<ApiResult<WorkflowTaskMutationResultDto>>> ReorderTask(Guid workflowJobId, [FromBody] ReorderWorkflowTaskRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowMutationService.ReorderTaskAsync(workflowJobId, request, cancellationToken);
        return Ok(ApiResult<WorkflowTaskMutationResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/{taskId:guid}/execute")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> ExecuteTask(Guid workflowJobId, Guid taskId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.ExecuteTaskAsync(workflowJobId, taskId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/{taskId:guid}/accept")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> AcceptTask(Guid workflowJobId, Guid taskId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.AcceptTaskAsync(workflowJobId, taskId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/{taskId:guid}/reset")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> ResetTask(Guid workflowJobId, Guid taskId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.ResetTaskAsync(workflowJobId, taskId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/tasks/{taskId:guid}/reject")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> RejectTask(Guid workflowJobId, Guid taskId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.RejectTaskAsync(workflowJobId, taskId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/pause")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> PauseWorkflow(Guid workflowJobId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.PauseWorkflowAsync(workflowJobId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }

    [HttpPost("workflows/{workflowJobId:guid}/resume")]
    public async Task<ActionResult<ApiResult<WorkflowControlResultDto>>> ResumeWorkflow(Guid workflowJobId, [FromBody] WorkflowTaskControlRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _workflowControlService.ResumeWorkflowAsync(workflowJobId, request, cancellationToken);
        return Ok(ApiResult<WorkflowControlResultDto>.Successful(result));
    }
}
