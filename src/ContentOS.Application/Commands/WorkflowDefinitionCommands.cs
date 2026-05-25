using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record ProposeWorkflowDefinitionChangeCommand(Guid WorkflowDefinitionId, string UserRequest) : IRequest<WorkflowDefinitionChangeProposalDto>;
public record ApplyWorkflowDefinitionChangeCommand(Guid WorkflowDefinitionId, WorkflowDefinitionChangeProposalDto Proposal, string CreatedBy) : IRequest<WorkflowDefinitionDto>;
public record SaveWorkflowDefinitionCommand(WorkflowDefinitionDto Definition, string CreatedBy) : IRequest<WorkflowDefinitionDto>;
