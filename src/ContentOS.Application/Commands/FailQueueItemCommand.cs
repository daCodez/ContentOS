using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record FailQueueItemCommand(Guid ItemId, string ErrorMessage) : IRequest;