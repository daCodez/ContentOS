using System.Threading;
using System.Threading.Tasks;

namespace ContentOS.Infrastructure.Image;

public interface IImageGenerationService
{
    Task<string> GenerateImageAsync(string prompt, int width, int height, string? outputFilePath = null, CancellationToken cancellationToken = default);
}