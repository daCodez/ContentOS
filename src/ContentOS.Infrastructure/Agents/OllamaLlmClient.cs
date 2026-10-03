using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ContentOS.Infrastructure.Workflow;

namespace ContentOS.Infrastructure.Agents;

/// <summary>Existing Ollama client with safe provider diagnostics; prompt, response and exception content is never logged.</summary>
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
        var diagnosticStarted = Stopwatch.GetTimestamp();
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
            WorkflowDiagnostics.ProviderResult(_logger, "StructuredLlm", model ?? "gemma4:31b-cloud", WorkflowDiagnostics.ConfigurationId(_httpClient.BaseAddress), diagnosticStarted, "invalid-json", failure: WorkflowDiagnostics.Failure(ex));
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
        var diagnosticStarted = Stopwatch.GetTimestamp();

        var payload = new
        {
            model = actualModel,
            prompt,
            stream = false,
            options = new { temperature = 0.7 }
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/generate", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            WorkflowDiagnostics.ProviderResult(_logger, "RawLlm", actualModel, WorkflowDiagnostics.ConfigurationId(_httpClient.BaseAddress), diagnosticStarted, string.IsNullOrWhiteSpace(result?.Response) ? "empty-output" : "response-received", (int)response.StatusCode);
            return result?.Response ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorkflowDiagnostics.ProviderResult(_logger, "RawLlm", actualModel, WorkflowDiagnostics.ConfigurationId(_httpClient.BaseAddress), diagnosticStarted, "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            WorkflowDiagnostics.ProviderResult(_logger, "RawLlm", actualModel, WorkflowDiagnostics.ConfigurationId(_httpClient.BaseAddress), diagnosticStarted, "failure", failure: WorkflowDiagnostics.Failure(ex));
            throw;
        }
    }

    private sealed class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
