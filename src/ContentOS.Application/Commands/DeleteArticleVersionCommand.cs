using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record DeleteArticleVersionCommand(Guid VersionId) : IRequest;
