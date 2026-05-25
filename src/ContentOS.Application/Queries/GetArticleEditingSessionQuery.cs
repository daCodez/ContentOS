using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetArticleEditingSessionQuery(Guid ArticleId) : IRequest<ArticleEditingSessionDto>;
