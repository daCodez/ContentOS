using System;
using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowJobByIdQuery(Guid WorkflowJobId) : IRequest<ContentWorkflowJob?>;
