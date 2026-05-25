using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContentOS.Infrastructure.Storage;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a stream to a local file and returns the relative web path.
    /// </summary>
    Task<string> SaveFileAsync(Stream stream, string fileName, string folder, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes a file by its relative web path.
    /// </summary>
    Task DeleteFileAsync(string relativePath);
    
    /// <summary>
    /// Returns the absolute path to a relative web path.
    /// </summary>
    string GetAbsolutePath(string relativePath);
}