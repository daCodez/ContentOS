using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Agents;

public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmClient> _logger;

    public OllamaLlmClient(HttpClient httpClient, ILogger<OllamaLlmClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<T?> GenerateAsync<T>(string prompt, string? model = null, CancellationToken cancellationToken = default)
    {
        var responseText = await GenerateRawAsync(prompt, model, cancellationToken);
        if (string.IsNullOrWhiteSpace(responseText)) return default;

        try
        {
            // Strip markdown code fences if present
            var json = responseText.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json["```json".Length..];
            else if (json.StartsWith("```", StringComparison.OrdinalIgnoreCase))
                json = json[3..];
            if (json.EndsWith("```", StringComparison.OrdinalIgnoreCase))
                json = json[..^3];
            json = json.Trim();

            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize LLM response to {Type}: {Response}", typeof(T).Name, responseText[..Math.Min(200, responseText.Length)]);
            return default;
        }
    }

    public async Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken cancellationToken = default)
    {
        return await GenerateRawAsync(prompt, model, cancellationToken);
    }

    private async Task<string> GenerateRawAsync(string prompt, string? model, CancellationToken cancellationToken)
    {
        var actualModel = model ?? "gemma4:31b-cloud";

        var payload = new
        {
            model = actualModel,
            prompt,
            stream = false,
            options = new { temperature = 0.7 }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama LLM call failed for model {Model}", actualModel);
            throw;
        }
    }

    private sealed class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}