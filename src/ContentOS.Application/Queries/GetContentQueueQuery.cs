using System.Collections.Generic;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetContentQueueQuery() : IRequest<IEnumerable<QueueItemDto>>;
