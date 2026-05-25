using System.Text.Json;

namespace ContentOS.Application.Abstractions;

public interface ILlmClient
{
    Task<T?> GenerateAsync<T>(string prompt, string? model = null, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken cancellationToken = default);
}