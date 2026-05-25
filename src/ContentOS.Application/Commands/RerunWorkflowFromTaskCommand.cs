using MediatR;

namespace ContentOS.Application.Commands;

public record RerunWorkflowFromTaskCommand(Guid WorkflowJobId, int StartingDisplayOrder, string RequestedBy) : IRequest<bool>;
