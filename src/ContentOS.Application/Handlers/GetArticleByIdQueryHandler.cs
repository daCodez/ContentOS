using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;
using MediatR;

namespace ContentOS.Application.Handlers;

public class GetArticleByIdQueryHandler : IRequestHandler<GetArticleByIdQuery, ArticleDto>
{
    private readonly IArticleRepository _articleRepository;

    public GetArticleByIdQueryHandler(IArticleRepository articleRepository)
    {
        _articleRepository = articleRepository;
    }

    public async Task<ArticleDto> Handle(GetArticleByIdQuery request, CancellationToken cancellationToken)
    {
        var article = await _articleRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (article == null)
        {
            return null;
        }
        
        return new ArticleDto
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            Summary = article.Summary,
            Author = article.Author,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            IsPublished = article.IsPublished,
            Status = article.Status
        };
    }
}