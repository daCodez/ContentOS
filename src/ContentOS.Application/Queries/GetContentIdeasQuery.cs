using System.Collections.Generic;
using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetContentIdeasQuery() : IRequest<IEnumerable<ContentIdea>>;
