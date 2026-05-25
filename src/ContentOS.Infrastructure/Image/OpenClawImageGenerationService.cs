using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Image;

public sealed class OpenClawImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenClawImageGenerationService> _logger;
    private readonly string _apiKey;

    public OpenClawImageGenerationService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenClawImageGenerationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["IMAGE_GEN_API_KEY"] ?? string.Empty;
    }

    public async Task<string> GenerateImageAsync(string prompt, int width, int height, string? outputFilePath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Image generation API key is missing. Using placeholder image.");
            return $"https://placehold.co/{width}x{height}?text={Uri.EscapeDataString(prompt.Length > 30 ? prompt[..30] + "..." : prompt)}";
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/generate", new
            {
                prompt = prompt,
                width = width,
                height = height,
                api_key = _apiKey
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Image generation request failed: {StatusCode}", response.StatusCode);
                return $"https://placehold.co/{width}x{height}?text=Error+Generating+Image";
            }

            var result = await response.Content.ReadFromJsonAsync<ImageGenResponse>(cancellationToken);
            return result?.Url ?? $"https://placehold.co/{width}x{height}?text=No+Url+Returned";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during image generation for prompt: {Prompt}", prompt);
            return $"https://placehold.co/{width}x{height}?text=Generation+Exception";
        }
    }

    private sealed record ImageGenResponse(string Url);
}
