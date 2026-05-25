using System;
using System.Collections.Generic;
using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowTasksByJobQuery(Guid WorkflowJobId) : IRequest<IEnumerable<ContentWorkflowTask>>;
