using System;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

/// <summary>
/// Assembles a ready-to-render article from a completed workflow job.
/// Finds the key task outputs (WriterAgent, HumanizerAgent, QaScoringAgent)
/// and stitches them into a single clean DTO.
/// </summary>
public record GetArticleAssemblyByWorkflowJobIdQuery(Guid WorkflowJobId) : IRequest<ArticleAssemblyDto>;