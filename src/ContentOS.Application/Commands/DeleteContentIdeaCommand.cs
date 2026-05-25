using MediatR;

namespace ContentOS.Application.Commands;

public record DeleteContentIdeaCommand(Guid ContentIdeaId) : IRequest;
