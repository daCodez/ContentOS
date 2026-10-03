using System;
using System.Threading.Tasks;

namespace ContentOS.Application.Notifications;

public class NullWorkflowNotificationService : IWorkflowNotificationService
{
	public Task NotifyRunUpdatedAsync(Guid runId)
	{
		return Task.CompletedTask;
	}
}
