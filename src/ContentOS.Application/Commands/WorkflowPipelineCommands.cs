using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record PublishWorkflowDefinitionVersionCommand(WorkflowDefinitionDto Definition, string PublishedBy) : IRequest<WorkflowDefinitionDto>;
public record ApproveIdeaRecordCommand(Guid IdeaRecordId, string ApprovedBy) : IRequest<Guid>;
public record RejectIdeaRecordCommand(Guid IdeaRecordId, string RejectedBy) : IRequest;
public record RetryWorkflowActionRunCommand(Guid WorkflowDefinitionRunId, Guid WorkflowActionRunId, string RequestedBy) : IRequest<bool>;
public record GenerateIdeasCommand(string Niche, string AudienceDescription = "", int MaxIdeas = 5) : IRequest;
