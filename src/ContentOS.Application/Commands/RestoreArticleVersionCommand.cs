using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record RestoreArticleVersionCommand(Guid ArticleId, Guid VersionId) : IRequest;