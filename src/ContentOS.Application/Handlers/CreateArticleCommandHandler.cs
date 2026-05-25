using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;
using MediatR;

namespace ContentOS.Application.Handlers;

public class CreateArticleCommandHandler : IRequestHandler<CreateArticleCommand, Guid>
{
    private readonly IArticleRepository _articleRepository;

    public CreateArticleCommandHandler(IArticleRepository articleRepository)
    {
        _articleRepository = articleRepository;
    }

    public async Task<Guid> Handle(CreateArticleCommand request, CancellationToken cancellationToken)
    {
        // Create article entity (simplified)
        var article = new ContentOS.Domain.Entities.Article
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Summary = request.Summary,
            Author = request.Author,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublished = false // Default to unpublished
        };

        await _articleRepository.AddAsync(article, cancellationToken);
        return article.Id;
    }
}