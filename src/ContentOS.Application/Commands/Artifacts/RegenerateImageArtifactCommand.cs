using MediatR;

namespace ContentOS.Application.Commands.Artifacts;

public record RegenerateImageArtifactCommand(Guid ArtifactId, string RequestedBy) : IRequest<bool>;
