using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record DeleteWorkflowCommand(Guid Id) : IRequest;