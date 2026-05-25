using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record UpdateArticleFieldLocksCommand(Guid ArticleId, ArticleFieldLocksDto Locks) : IRequest<ArticleFieldLocksDto>;
