using ContentOS.Application.Research;
using MediatR;

namespace ContentOS.Application.Commands;

public record RunResearchCommand(Guid SiteId, int? RequestedIdeaCount = null) : IRequest<RunResearchResult>;
