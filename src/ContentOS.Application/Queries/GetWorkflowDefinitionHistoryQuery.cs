using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowDefinitionHistoryQuery(Guid WorkflowDefinitionId) : IRequest<IEnumerable<WorkflowDefinitionMutationHistoryDto>>;
public record GetWorkflowJobVersionInfoQuery(Guid WorkflowJobId) : IRequest<WorkflowJobVersionInfoDto?>;
