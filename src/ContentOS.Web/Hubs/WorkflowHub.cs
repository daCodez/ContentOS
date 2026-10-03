using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ContentOS.Web.Hubs;

public class WorkflowHub : Hub
{
	public async Task JoinRunGroup(string runId)
	{
		await base.Groups.AddToGroupAsync(base.Context.ConnectionId, runId);
	}

	public async Task LeaveRunGroup(string runId)
	{
		await base.Groups.RemoveFromGroupAsync(base.Context.ConnectionId, runId);
	}

	public async Task JoinDashboard()
	{
		await base.Groups.AddToGroupAsync(base.Context.ConnectionId, "dashboard");
	}

	public async Task LeaveDashboard()
	{
		await base.Groups.RemoveFromGroupAsync(base.Context.ConnectionId, "dashboard");
	}
}
