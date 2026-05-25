using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record ProcessQueueCommand(Guid ItemId) : IRequest;