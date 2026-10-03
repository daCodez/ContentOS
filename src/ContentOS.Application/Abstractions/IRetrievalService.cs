using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContentOS.Application.Abstractions;

public interface IRetrievalService
{
	bool IsAvailable { get; }

	Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(RetrievalQuery query, CancellationToken ct = default(CancellationToken));

	Task IndexAsync(string collection, string slug, string markdownContent, CancellationToken ct = default(CancellationToken));

	Task RemoveAsync(string collection, string slug, CancellationToken ct = default(CancellationToken));
}
