using System.Text.Json;
using ContentOS.Application.Commands.Artifacts;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Image;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure.Handlers;

public class RegenerateImageArtifactCommandHandler : IRequestHandler<RegenerateImageArtifactCommand, bool>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly IImageGenerationService _imageGenerationService;

    public RegenerateImageArtifactCommandHandler(ContentOsDbContext dbContext, IImageGenerationService imageGenerationService)
    {
        _dbContext = dbContext;
        _imageGenerationService = imageGenerationService;
    }

    public async Task<bool> Handle(RegenerateImageArtifactCommand request, CancellationToken cancellationToken)
    {
        var artifact = await _dbContext.ContentArtifacts.FirstOrDefaultAsync(x => x.Id == request.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return false;
        }

        if (artifact.ArtifactType is not ("FeaturedImageAsset" or "SupportingImageAsset"))
        {
            return false;
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(artifact.ContentJson) ? "{}" : artifact.ContentJson);
        var root = document.RootElement;

        var prompt = GetString(root, "prompt");
        var width = GetInt(root, "width", 1024);
        var height = GetInt(root, "height", 1024);
        var altText = GetString(root, "altText");
        var title = GetString(root, "title");
        var imageRole = GetString(root, "imageRole");
        var suggestedPlacement = GetString(root, "suggestedPlacement");
        var sourceTask = GetString(root, "sourceTask");
        var aspectRatio = GetString(root, "aspectRatio");

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var imagePath = await _imageGenerationService.GenerateImageAsync(prompt, width, height, cancellationToken: cancellationToken);

        artifact.ContentJson = JsonSerializer.Serialize(new
        {
            title,
            imageRole,
            status = "Regenerated",
            prompt,
            altText,
            aspectRatio,
            width,
            height,
            sourceTask,
            generated = true,
            imagePath,
            suggestedPlacement,
            regeneratedUtc = DateTime.UtcNow,
            regeneratedBy = request.RequestedBy
        }, new JsonSerializerOptions { WriteIndented = true });
        artifact.ContentText = prompt;
        artifact.VersionNumber += 1;
        artifact.CreatedUtc = DateTime.UtcNow;
        artifact.CreatedByAgent = request.RequestedBy;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement root, string name, int fallback)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;
}
