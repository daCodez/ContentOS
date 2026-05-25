using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Domain.Entities;

namespace ContentOS.Domain.Repositories;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Workflow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}