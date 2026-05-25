using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Image;

[ExcludeFromCodeCoverage]
public sealed class FreePollinationsImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FreePollinationsImageService> _logger;
    private readonly IFileStorageService _fileStorage;

    public FreePollinationsImageService(HttpClient httpClient, ILogger<FreePollinationsImageService> logger, IFileStorageService fileStorage)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileStorage = fileStorage;
    }

    public async Task<string> GenerateImageAsync(string prompt, int width, int height, string? outputFilePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Pollinations.ai allows image generation via a simple GET URL.
            // We encode the prompt and pass dimensions as parameters.
            var encodedPrompt = Uri.EscapeDataString(prompt);
            var imageUrl = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&nologo=true";

            _logger.LogInformation("Generating free image via Pollinations.ai for prompt: {Prompt}", prompt);
            
            // Download the image from the URL
            var response = await _httpClient.GetAsync(imageUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            // Read the image as a stream
            var imageStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            
            // Generate a file name for the saved image
            var fileName = $"{Guid.NewGuid()}.png";
            
            // Save the image to local storage (under wwwroot/images/artifacts)
            var relativePath = await _fileStorage.SaveFileAsync(imageStream, fileName, "artifacts", cancellationToken);
            
            _logger.LogInformation("Image saved to local path: {Path}", relativePath);
            
            // Return the relative path (will be served by our own server)
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during free image generation for prompt: {Prompt}", prompt);
            // Fallback to a placeholder image (we'll use a local placeholder or a remote one)
            // For now, return a placeholder URL (we can change to a local placeholder later)
            return $"https://placehold.co/{width}x{height}?text=Image+Gen+Error";
        }
    }
}