using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using MediatR;

namespace ContentOS.Application.Commands;

public record CreateWorkflowCommand(string Name, string Description, List<Guid> ArticleIds) : IRequest<WorkflowDto>;