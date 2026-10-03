using System;
using System.Threading.Tasks;

namespace ContentOS.Application.Notifications;

public interface IWorkflowNotificationService
{
	Task NotifyRunUpdatedAsync(Guid runId);
}
