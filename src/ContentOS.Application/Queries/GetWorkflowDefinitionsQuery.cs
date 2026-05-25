using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowDefinitionsQuery() : IRequest<IEnumerable<WorkflowDefinitionDto>>;
public record GetWorkflowDefinitionByIdQuery(Guid WorkflowDefinitionId) : IRequest<WorkflowDefinitionDto?>;
