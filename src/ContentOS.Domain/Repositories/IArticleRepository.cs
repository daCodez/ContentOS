using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Domain.Entities;

namespace ContentOS.Domain.Repositories;

public interface IArticleRepository
{
    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Article>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Article article, CancellationToken cancellationToken = default);
    Task UpdateAsync(Article article, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}