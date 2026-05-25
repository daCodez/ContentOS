using System;
using System.Collections.Concurrent;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Repositories;
using WorkflowEntity = ContentOS.Domain.Entities.Workflow;

namespace ContentOS.Infrastructure;

public sealed class InMemoryWorkflowRepository : IWorkflowRepository
{
    private static readonly ConcurrentDictionary<Guid, WorkflowEntity> Store = new();

    public Task<WorkflowEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Store.TryGetValue(id, out var workflow);
        return Task.FromResult(workflow);
    }

    public Task<IEnumerable<WorkflowEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<WorkflowEntity>>(Store.Values.OrderByDescending(w => w.CreatedAt).ToList());
    }

    public Task AddAsync(WorkflowEntity workflow, CancellationToken cancellationToken = default)
    {
        Store[workflow.Id] = workflow;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkflowEntity workflow, CancellationToken cancellationToken = default)
    {
        Store[workflow.Id] = workflow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
