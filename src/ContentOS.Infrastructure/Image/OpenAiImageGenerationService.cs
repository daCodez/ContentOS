using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Image;

[ExcludeFromCodeCoverage]
public sealed class OpenAiImageGenerationService : IImageGenerationService
{
	private sealed record OpenAiImageResponse(List<OpenAiImageData>? Data);

	private sealed record OpenAiImageData(string? Url, string? B64Json, string? RevisedPrompt);

	private readonly HttpClient _httpClient;

	private readonly IFileStorageService _fileStorageService;

	private readonly ILogger<OpenAiImageGenerationService> _logger;

	private readonly string _apiKey;

	public OpenAiImageGenerationService(HttpClient httpClient, IFileStorageService fileStorageService, IConfiguration configuration, ILogger<OpenAiImageGenerationService> logger)
	{
		_httpClient = httpClient;
		_fileStorageService = fileStorageService;
		_logger = logger;
		_apiKey = configuration["OPENAI_API_KEY"] ?? configuration["OpenAI:ApiKey"] ?? string.Empty;
	}

	public async Task<string> GenerateImageAsync(string prompt, int width, int height, string? outputFilePath = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(_apiKey))
		{
			_logger.LogWarning("OpenAI API key is missing (OPENAI_API_KEY). Falling back to placeholder image.");
			return await SavePlaceholderImageAsync(prompt, width, height, cancellationToken);
		}
		var (model, size) = MapToSupportedSize(width, height);
		try
		{
			_logger.LogInformation("Generating image via OpenAI {Model} ({Size}) for prompt: {Prompt}", model, size, prompt);
			var requestBody = new
			{
				model = model,
				prompt = prompt,
				n = 1,
				size = size,
				quality = "medium"
			};
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "v1/images/generations");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
			request.Content = JsonContent.Create(requestBody);
			HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
				_logger.LogError("OpenAI image generation failed: {StatusCode} - {Error}", response.StatusCode, errorBody);
				return await SavePlaceholderImageAsync(prompt, width, height, cancellationToken);
			}
			OpenAiImageData imageData = (await response.Content.ReadFromJsonAsync<OpenAiImageResponse>(cancellationToken))?.Data?.FirstOrDefault();
			if (imageData?.B64Json != null)
			{
				byte[] bytes = Convert.FromBase64String(imageData.B64Json);
				string fileName = $"img_{Guid.NewGuid():N}.png";
				using MemoryStream stream = new MemoryStream(bytes);
				string savedPath = await _fileStorageService.SaveFileAsync(stream, fileName, "images", cancellationToken);
				_logger.LogInformation("Image saved locally via b64_json at: {Path}", savedPath);
				return savedPath;
			}
			if (imageData?.Url != null)
			{
				string imageUrl = imageData.Url;
				_logger.LogInformation("Downloading generated image from URL: {Url}", imageUrl);
				using HttpResponseMessage downloadResponse = await _httpClient.GetAsync(imageUrl, cancellationToken);
				if (downloadResponse.IsSuccessStatusCode)
				{
					byte[] imageBytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
					string fileName2 = $"img_{Guid.NewGuid():N}.png";
					using MemoryStream imgStream = new MemoryStream(imageBytes);
					string savedPath2 = await _fileStorageService.SaveFileAsync(imgStream, fileName2, "images", cancellationToken);
					_logger.LogInformation("Image downloaded and saved locally at: {Path}", savedPath2);
					return savedPath2;
				}
				_logger.LogWarning("Failed to download generated image from URL: {StatusCode}", downloadResponse.StatusCode);
				return imageUrl;
			}
			_logger.LogError("OpenAI returned no image data in response.");
			return await SavePlaceholderImageAsync(prompt, width, height, cancellationToken);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Exception during OpenAI image generation for prompt: {Prompt}", prompt);
			return await SavePlaceholderImageAsync(prompt, width, height, cancellationToken);
		}
	}

	private async Task<string> SavePlaceholderImageAsync(string prompt, int width, int height, CancellationToken cancellationToken)
	{
		try
		{
			string placeholderUrl = $"https://placehold.co/{width}x{height}/e2e8f0/475569?text={Uri.EscapeDataString((prompt.Length > 30) ? (prompt.Substring(0, 30) + "...") : prompt)}";
			using HttpResponseMessage response = await _httpClient.GetAsync(placeholderUrl, cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
				string fileName = $"img_{Guid.NewGuid():N}.png";
				using MemoryStream stream = new MemoryStream(bytes);
				string savedPath = await _fileStorageService.SaveFileAsync(stream, fileName, "images", cancellationToken);
				_logger.LogInformation("Placeholder image saved locally at: {Path}", savedPath);
				return savedPath;
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_logger.LogWarning(ex2, "Failed to save placeholder image locally.");
		}
		return $"https://placehold.co/{width}x{height}/e2e8f0/475569?text={Uri.EscapeDataString((prompt.Length > 30) ? (prompt.Substring(0, 30) + "...") : prompt)}";
	}

	private static (string model, string size) MapToSupportedSize(int width, int height)
	{
		if (width > height)
		{
			double num = (double)width / (double)height;
			if (num >= 1.4)
			{
				return (model: "gpt-image-2", size: "1536x1024");
			}
			return (model: "dall-e-3", size: "1792x1024");
		}
		if (height > width)
		{
			double num2 = (double)height / (double)width;
			if (num2 >= 1.4)
			{
				return (model: "gpt-image-2", size: "1024x1536");
			}
			return (model: "dall-e-3", size: "1024x1792");
		}
		return (model: "gpt-image-2", size: "1024x1024");
	}
}
