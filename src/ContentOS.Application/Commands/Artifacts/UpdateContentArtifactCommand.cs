using MediatR;

namespace ContentOS.Application.Commands.Artifacts;

public record UpdateContentArtifactCommand(
    Guid ArtifactId,
    string ContentJson,
    string ContentText,
    string UpdatedBy) : IRequest<bool>;
