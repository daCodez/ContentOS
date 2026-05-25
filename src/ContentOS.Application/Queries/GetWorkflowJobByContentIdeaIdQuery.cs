using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowJobByContentIdeaIdQuery(Guid ContentIdeaId) : IRequest<ContentWorkflowJob?>;
