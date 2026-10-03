using ContentOS.Infrastructure.Workflow;
namespace ContentOS.Infrastructure.Articles;

public sealed record ReviewedPublishedSiteInventory(Guid SiteId,string SiteUrl,string ReviewSource,DateTime ReviewedUtc,IReadOnlyList<PublishedArticleTarget> Targets);
/// <summary>Explicit reviewed published-URL inventory plus fresh public-page verification. Never infers URLs from slugs or draft tables.</summary>
public sealed class ReviewedPublishedArticleInventory(IReadOnlyList<ReviewedPublishedSiteInventory> reviewed,IArticlePublicPageReader pages):IPublishedArticleInventory
{
    public async Task<IReadOnlyList<PublishedArticleTarget>> ReadAsync(Guid siteId,CancellationToken cancellationToken)
    {
        var source=reviewed.SingleOrDefault(s=>s.SiteId==siteId)??throw new InvalidOperationException("No reviewed published URL inventory exists for the selected site.");
        var site=ArticlePublicPageReader.ValidateUrl(source.SiteUrl);
        if(string.IsNullOrWhiteSpace(source.ReviewSource)||source.ReviewedUtc>DateTime.UtcNow||source.ReviewedUtc<DateTime.UtcNow.AddDays(-7)||source.Targets.Count>100)throw new InvalidOperationException("Published inventory review provenance is absent, stale or exceeds its supported bound.");
        var targets=new List<PublishedArticleTarget>();
        foreach(var target in source.Targets)
        {
            var uri=ArticlePublicPageReader.ValidateUrl(target.Url);
            if(!uri.Host.Equals(site.Host,StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(target.Title)||string.IsNullOrWhiteSpace(target.Summary))throw new InvalidOperationException("Reviewed published target is not an identified article on the selected site.");
            var page=await pages.ReadAsync(target.Url,cancellationToken);
            if(page.IsFixture||page.HttpStatus<200||page.HttpStatus>=300||!new Uri(page.FinalUrl).Host.Equals(site.Host,StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(page.CleanText)||!page.CleanText.Contains(target.Title,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Reviewed published article is no longer accessible or its content identity differs.");
            targets.Add(target with{VerifiedPublishedUtc=page.CollectedUtc});
        }
        return targets;
    }
}
