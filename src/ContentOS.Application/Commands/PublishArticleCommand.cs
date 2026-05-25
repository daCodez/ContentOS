using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record PublishArticleCommand(Guid ArticleId) : IRequest;