using ContentOS.Infrastructure.Workflow;
using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.Abstractions;
using ContentOS.Infrastructure.Research.SearXng;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.Extraction;

public class ContentExtractorAdapter : IContentExtractor
{
	private readonly SearXngStructuredExtractor _inner;

	private readonly ILogger<ContentExtractorAdapter> _logger;

	public ContentExtractorAdapter(SearXngStructuredExtractor inner, ILogger<ContentExtractorAdapter> logger)
	{
		_inner = inner;
		_logger = logger;
	}

	public async Task<ExtractionResult?> ExtractAsync(string url, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}
		try
		{
			ExtractedContent extracted = await _inner.ExtractAsync(url, ct);
			if (extracted == null)
			{
				return null;
			}
			return new ExtractionResult
			{
				Title = extracted.Title,
				CleanedText = extracted.CleanedText,
				Headings = extracted.Headings,
				BulletPoints = extracted.BulletPoints,
				SourceUrl = url,
				ExtractedAt = DateTime.UtcNow
			};
		}
		catch (Exception exception)
		{
			WorkflowDiagnostics.LogFailure(_logger, exception, "Source extraction adapter failed. Review the source before retrying.", WorkflowDiagnostics.SourceId(url));
			return null;
		}
	}
}
