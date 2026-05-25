using System;
using System.Collections.Generic;
using MediatR;

namespace ContentOS.Application.Commands;

public record UpdateWorkflowCommand(Guid Id, string? Name, string? Description, bool? IsActive, IEnumerable<Guid>? ArticleIds) : IRequest;