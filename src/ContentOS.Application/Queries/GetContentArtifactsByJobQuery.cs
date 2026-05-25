using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetContentArtifactsByJobQuery(Guid WorkflowJobId) : IRequest<IEnumerable<ContentArtifact>>;
