using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetContentResearchSourcesByIdeaIdQuery(Guid ContentIdeaId) : IRequest<IEnumerable<ContentResearchSource>>;
