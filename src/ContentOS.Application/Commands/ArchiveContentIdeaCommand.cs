using MediatR;

namespace ContentOS.Application.Commands;

public record ArchiveContentIdeaCommand(Guid ContentIdeaId) : IRequest;
