using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream stream, string fileName, string folder, CancellationToken ct = default)
    {
        try
        {
            var root = Path.Combine(_env.ContentRootPath, "wwwroot", "images", folder);
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);

            // Sanitize filename and add GUID to prevent collisions
            var safeName = Path.GetRandomFileName() + Path.GetExtension(fileName);
            var fullPath = Path.Combine(root, safeName);

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await stream.CopyToAsync(fileStream, ct);

            var relativePath = $"/images/{folder}/{safeName}";
            _logger.LogInformation("File saved to {Path}", relativePath);
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file {FileName}", fileName);
            throw;
        }
    }

    public async Task DeleteFileAsync(string relativePath)
    {
        var absPath = GetAbsolutePath(relativePath);
        if (File.Exists(absPath))
        {
            await Task.Run(() => File.Delete(absPath));
        }
    }

    public string GetAbsolutePath(string relativePath)
    {
        // Strip leading slash for Path.Combine
        var trimmedPath = relativePath.TrimStart('/');
        return Path.Combine(_env.ContentRootPath, "wwwroot", trimmedPath);
    }
}