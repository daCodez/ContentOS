using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowJobListQuery() : IRequest<IEnumerable<WorkflowJobListItemDto>>;
