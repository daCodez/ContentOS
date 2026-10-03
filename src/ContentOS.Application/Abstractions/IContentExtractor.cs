using System.Threading;
using System.Threading.Tasks;

namespace ContentOS.Application.Abstractions;

public interface IContentExtractor
{
	Task<ExtractionResult?> ExtractAsync(string url, CancellationToken ct = default(CancellationToken));
}
