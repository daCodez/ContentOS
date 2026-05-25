using System;
using System.Collections.Concurrent;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;

namespace ContentOS.Infrastructure;

public sealed class InMemoryArticleRepository : IArticleRepository
{
    private static readonly ConcurrentDictionary<Guid, Article> Store = new();

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Store.TryGetValue(id, out var article);
        return Task.FromResult(article);
    }

    public Task<IEnumerable<Article>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Article>>(Store.Values.OrderByDescending(a => a.CreatedAt).ToList());
    }

    public Task AddAsync(Article article, CancellationToken cancellationToken = default)
    {
        Store[article.Id] = article;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Article article, CancellationToken cancellationToken = default)
    {
        Store[article.Id] = article;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
