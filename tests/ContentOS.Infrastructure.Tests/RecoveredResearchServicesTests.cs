using ContentOS.Application.Abstractions;
using ContentOS.Infrastructure.Research.Extraction;
using ContentOS.Infrastructure.Research.Retrieval;
using ContentOS.Infrastructure.Research.SearXng;
using ContentOS.Infrastructure.Research.SeoData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class RecoveredResearchServicesTests
{
    [Test]
    public void ResearchServicesResolveThroughTheirOriginalContracts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IContentExtractor>(), Is.TypeOf<ContentExtractorAdapter>());
        Assert.That(provider.GetRequiredService<IRetrievalService>(), Is.TypeOf<QmdRetrievalService>());
        Assert.That(provider.GetRequiredService<SeoDataAggregator>(), Is.Not.Null);
        Assert.That(provider.GetServices<ISeoDataProvider>().Select(p => p.ProviderName), Does.Contain("Google Trends"));
    }

    [Test]
    public void StructuredExtractionPreservesHeadingsAndRemovesScripts()
    {
        var result = SearXngStructuredExtractor.ParseHtml("<html><head><title>Research example</title></head><body><h1>Budgeting guide</h1><p>Helpful advice for a monthly household budget.</p><ul><li>Track spending</li></ul><script>privateScriptValue()</script></body></html>");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Research example"));
        Assert.That(result.Headings, Does.Contain("Budgeting guide"));
        Assert.That(result.BulletPoints, Does.Contain("Track spending"));
        Assert.That(result.CleanedText, Does.Not.Contain("privateScriptValue"));
    }

    [Test]
    public async Task AggregatorReturnsAvailableProviderDataWithoutInventingMissingMetrics()
    {
        var aggregator = new SeoDataAggregator(Array.Empty<ISeoDataProvider>(), NullLogger<SeoDataAggregator>.Instance);
        Assert.That(await aggregator.GetKeywordMetricsAsync("budgeting"), Is.Null);
        Assert.That(await aggregator.GetRelatedKeywordsAsync("budgeting"), Is.Empty);
    }
}
