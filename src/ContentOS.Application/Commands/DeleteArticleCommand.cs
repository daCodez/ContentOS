using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record DeleteArticleCommand(Guid Id) : IRequest;