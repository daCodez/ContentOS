using System;
using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetContentIdeaByIdQuery(Guid Id) : IRequest<ContentIdea?>;