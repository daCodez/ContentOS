using System;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetArticleByIdQuery(Guid Id) : IRequest<ArticleDto>;