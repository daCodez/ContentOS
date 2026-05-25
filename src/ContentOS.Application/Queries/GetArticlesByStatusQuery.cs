using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using System;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetArticlesByStatusQuery(string Status) : IRequest<IEnumerable<ArticleDto>>;