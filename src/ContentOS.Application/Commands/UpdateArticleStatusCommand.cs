using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record UpdateArticleStatusCommand(Guid Id, string Status) : IRequest;