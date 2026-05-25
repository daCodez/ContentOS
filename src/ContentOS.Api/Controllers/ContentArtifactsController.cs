using ContentOS.Api.Responses;
using ContentOS.Application.Commands.Artifacts;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Storage;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/artifacts")]
[Produces("application/json")]
[Consumes("application/json")]
public class ContentArtifactsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;
    private readonly ContentOS.Infrastructure.ContentOsDbContext _dbContext;

    public ContentArtifactsController(IMediator mediator, IFileStorageService fileStorage, ContentOS.Infrastructure.ContentOsDbContext dbContext)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
        _dbContext = dbContext;
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContentArtifactRequest request, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new UpdateContentArtifactCommand(
            id,
            request.ContentJson,
            request.ContentText,
            request.UpdatedBy), cancellationToken);

        return success
            ? Ok(ApiResult<object>.Successful(new { artifactId = id }, "Artifact updated."))
            : NotFound(ApiResult<object>.Failed("Artifact not found."));
    }

    [HttpPost("{id:guid}/regenerate-image")]
    public async Task<IActionResult> RegenerateImage(Guid id, [FromBody] RegenerateImageArtifactRequest request, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new RegenerateImageArtifactCommand(id, request.RequestedBy), cancellationToken);

        return success
            ? Ok(ApiResult<object>.Successful(new { artifactId = id }, "Image regenerated."))
            : NotFound(ApiResult<object>.Failed("Image artifact not found or cannot be regenerated."));
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] Guid jobId, [FromForm] string requestedBy, CancellationToken cancellationToken)
    {
        // Validate file
        if (file == null || file.Length == 0)
            return BadRequest(ApiResult<object>.Failed("No file uploaded."));

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest(ApiResult<object>.Failed("File is not an image."));

        try
        {
            // Save the file to local storage
            var relativePath = await _fileStorage.SaveFileAsync(file.OpenReadStream(), file.FileName, "artifacts", cancellationToken);

            // Create a ContentArtifact record for the uploaded image
            var artifact = new ContentOS.Domain.Entities.ContentArtifact
            {
                Id = Guid.NewGuid(),
                ContentWorkflowJobId = jobId,
                ArtifactType = "SupportingImageAsset",
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                BlobPath = relativePath, // Store the relative path
                ContentJson = JsonSerializer.Serialize(new
                {
                    title = Path.GetFileNameWithoutExtension(file.FileName),
                    imageRole = "UploadedImage",
                    status = "Generated",
                    altText = Path.GetFileNameWithoutExtension(file.FileName), // Default alt text to filename
                    imagePath = relativePath,
                    suggestedPlacement = "after-section-1" // Default placement
                }),
                ContentText = null,
                CreatedByAgent = requestedBy,
                CreatedUtc = DateTime.UtcNow
            };

            _dbContext.ContentArtifacts.Add(artifact);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult<object>.Successful(new { 
                artifactId = artifact.Id,
                relativePath 
            }, "Image uploaded."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<object>.Failed($"Failed to upload image: {ex.Message}"));
        }
    }

    public sealed record UpdateContentArtifactRequest(string ContentJson, string ContentText, string UpdatedBy);
    public sealed record RegenerateImageArtifactRequest(string RequestedBy);
    public sealed record UploadImageResult(Guid ArtifactId, string RelativePath);
}