using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record ApproveContentIdeaCommand(Guid ContentIdeaId, string ApprovedBy) : IRequest<Guid>;