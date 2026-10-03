using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Articles;
namespace ContentOS.Infrastructure.Research.Providers;

/// <summary>Genuinely fetches reviewed public product pages on the selected site; never substitutes a prepared research packet.</summary>
public sealed class SiteOwnedContextResearchProvider(IArticlePublicPageReader reader,IReadOnlyList<string> publicContextUrls):IResearchSourceProvider
{
    public string Name=>"SiteOwnedContextResearchProvider";
    public async Task<IReadOnlyCollection<ResearchFinding>> ResearchAsync(ResearchContext context,CancellationToken cancellationToken)
    {
        var site=ArticlePublicPageReader.ValidateUrl(context.SiteUrl);var findings=new List<ResearchFinding>();
        if(publicContextUrls.Count is <1 or >8)throw new InvalidOperationException("Reviewed site context requires 1-8 public URLs.");
        foreach(var url in publicContextUrls)
        {
            if(!ArticlePublicPageReader.ValidateUrl(url).Host.Equals(site.Host,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Site context URL is outside the selected domain.");
            var page=await reader.ReadAsync(url,cancellationToken);
            if(page.IsFixture||page.HttpStatus<200||page.HttpStatus>=300||!new Uri(page.FinalUrl).Host.Equals(site.Host,StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(page.CleanText))throw new InvalidOperationException("Public product context page is unavailable, redirected off-site or not real readable evidence.");
            findings.Add(new(){ProviderName=Name,SourceType="SiteOwnedContext",SourceTitle="Public product documentation: "+new Uri(url).AbsolutePath,SourceUrl=url,SourceExcerpt=page.CleanText[..Math.Min(page.CleanText.Length,3500)],
                Notes=$"Actual public page fetched {page.CollectedUtc:O}; content hash {page.ContentHash}. Describes public product claims, not an independent test of deployed capabilities. Illustrative scores/examples are not real measurements. Coverage is limited to the provided excerpt."});
        }
        return findings;
    }
}
