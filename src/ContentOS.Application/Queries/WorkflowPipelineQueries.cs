using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowDefinitionFamiliesQuery() : IRequest<IEnumerable<WorkflowDefinitionFamilyDto>>;
public record GetWorkflowDefinitionVersionsQuery(Guid WorkflowDefinitionFamilyId) : IRequest<IEnumerable<WorkflowDefinitionDto>>;
public record GetIdeaQueueQuery() : IRequest<IEnumerable<IdeaRecordDto>>;
public record GetWorkflowDefinitionRunQuery(Guid RunId) : IRequest<WorkflowDefinitionRunDto?>;
public record GetArticleRunsQuery() : IRequest<IEnumerable<WorkflowDefinitionRunDto>>;
