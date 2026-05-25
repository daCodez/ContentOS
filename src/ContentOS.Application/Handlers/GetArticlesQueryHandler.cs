using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;
using MediatR;

namespace ContentOS.Application.Handlers;

public class GetArticlesQueryHandler : IRequestHandler<GetArticlesQuery, PaginatedResultDto<ArticleDto>>
{
    private readonly IArticleRepository _articleRepository;

    public GetArticlesQueryHandler(IArticleRepository articleRepository)
    {
        _articleRepository = articleRepository;
    }

    public async Task<PaginatedResultDto<ArticleDto>> Handle(GetArticlesQuery request, CancellationToken cancellationToken)
    {
        var articles = await _articleRepository.GetAllAsync(cancellationToken);
        var articleDtos = articles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            Summary = a.Summary,
            Author = a.Author,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            IsPublished = a.IsPublished,
            Status = a.Status
        });
        
        return new PaginatedResultDto<ArticleDto>
        {
            Items = articleDtos,
            TotalCount = articleDtos.Count(),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}