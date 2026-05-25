using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure;

public class WorkflowCoordinatorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowCoordinatorBackgroundService> _logger;

    public WorkflowCoordinatorBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WorkflowCoordinatorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow coordinator background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var newRuntime = scope.ServiceProvider.GetRequiredService<INewWorkflowRuntimeEngine>();
                await newRuntime.ProcessPendingRunsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow coordinator background cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("Workflow coordinator background service stopped.");
    }
}
