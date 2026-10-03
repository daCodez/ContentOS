using System;
using System.Threading.Tasks;
using ContentOS.Application.Notifications;
using ContentOS.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ContentOS.Web.Services;

public class WorkflowNotificationService : IWorkflowNotificationService
{
	private readonly IHubContext<WorkflowHub> _hubContext;

	public event Func<Guid, Task>? RunUpdated;

	public WorkflowNotificationService(IHubContext<WorkflowHub> hubContext)
	{
		_hubContext = hubContext;
	}

	public async Task NotifyRunUpdatedAsync(Guid runId)
	{
		await _hubContext.Clients.Group(runId.ToString()).SendAsync("RunUpdated");
		await _hubContext.Clients.Group("dashboard").SendAsync("DashboardRefresh", runId.ToString());
		if (RunUpdated != null)
		{
			await RunUpdated(runId);
		}
	}
}
