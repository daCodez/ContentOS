using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Writing;
using System.Text.RegularExpressions;
namespace ContentOS.Infrastructure.Articles;

public sealed record ReviewedMaterialClaim(string Text,string SourceUrl,string CollectedExcerpt,string ReviewReason,bool Supported);
public sealed record ReviewedArithmetic(string Expression,decimal ExpectedValue,string ReviewReason,string DisplayedEquation="");
public sealed record ReviewedArticleLink(string Url,int HttpStatus,DateTime CheckedUtc,string FinalUrl);
public sealed record CollectedArticleSourceEvidence(string Url,string FinalUrl,DateTime CollectedUtc,string Collector,string FullContentHash,string ProvidedExcerpt,string ProvidedExcerptHash);
public sealed record ArticleVerificationReport(string ArticleVersionHash,string Reviewer,string ReviewKind,DateTime ReviewedUtc,
    bool CompleteMaterialClaimInventory,IReadOnlyList<ReviewedMaterialClaim> Claims,IReadOnlyList<ReviewedArithmetic> Arithmetic,
    string? NoArithmeticReason,IReadOnlyList<ReviewedArticleLink> Links,IReadOnlyList<string> UnresolvedFailures,bool IsFixture=false,IReadOnlyList<CollectedArticleSourceEvidence>? CollectedSources=null);
public interface IArticleEvidenceReviewer
{
    Task<ArticleVerificationReport> ReviewAsync(GeneratedLongformArticle article,IReadOnlyList<string> requiredSources,CancellationToken cancellationToken);
}
public sealed class ArticlePostEditVerificationService(IArticleEvidenceReviewer reviewer)
{
    public async Task<ArticleVerificationReport> VerifyAsync(GeneratedLongformArticle article,IReadOnlyList<string> requiredSources,bool fixture,CancellationToken cancellationToken)
    {
        var hash=EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article));
        var report=await reviewer.ReviewAsync(article,requiredSources,cancellationToken);
        Validate(article,requiredSources,report,fixture,hash);return report;
    }
    public static void Validate(GeneratedLongformArticle article,IReadOnlyList<string> requiredSources,ArticleVerificationReport report,bool fixture,string? expectedHash=null)
    {
        var hash=expectedHash??EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article));
        if(report.ArticleVersionHash!=hash||string.IsNullOrWhiteSpace(report.Reviewer)||string.IsNullOrWhiteSpace(report.ReviewKind)
            ||report.ReviewedUtc<DateTime.UtcNow.AddHours(-24)||report.ReviewedUtc>DateTime.UtcNow.AddMinutes(5)||report.IsFixture&&!fixture)
            throw new InvalidOperationException("Post-edit review is absent, stale, unidentified or for a different article version.");
        if(!report.CompleteMaterialClaimInventory||report.UnresolvedFailures.Count>0||report.Claims.Count==0)
            throw new InvalidOperationException("Incomplete material claim review or unresolved factual failures block delivery.");
        var text=VerificationText(article);
        if(!fixture&&(report.CollectedSources is null||requiredSources.Any(url=>!report.CollectedSources.Any(s=>s.Url==url))))throw new InvalidOperationException("Production source review lacks actual collection provenance for every required source.");
        if(report.CollectedSources is not null&&report.CollectedSources.Any(s=>string.IsNullOrWhiteSpace(s.Collector)||s.FullContentHash.Length!=64||s.CollectedUtc<report.ReviewedUtc.AddMinutes(-30)||s.ProvidedExcerptHash!=FinalArticleDelivery.Hash(System.Text.Encoding.UTF8.GetBytes(s.ProvidedExcerpt))))throw new InvalidOperationException("Collected source provenance is stale or its excerpt identity differs.");
        if(report.Claims.Any(c=>!c.Supported||string.IsNullOrWhiteSpace(c.Text)||!text.Contains(c.Text,StringComparison.Ordinal)
            ||!requiredSources.Contains(c.SourceUrl,StringComparer.Ordinal)||string.IsNullOrWhiteSpace(c.CollectedExcerpt)||string.IsNullOrWhiteSpace(c.ReviewReason)))
            throw new InvalidOperationException("Claim review lacks actual article text, collected source evidence or an explicit support judgment.");
        if(report.CollectedSources is not null&&report.Claims.Any(c=>!report.CollectedSources.Any(s=>s.Url==c.SourceUrl&&s.ProvidedExcerpt.Contains(c.CollectedExcerpt,StringComparison.Ordinal))))throw new InvalidOperationException("A reviewed claim quote is absent from its persisted collected source excerpt.");
        if(report.Arithmetic.Count==0&&string.IsNullOrWhiteSpace(report.NoArithmeticReason))throw new InvalidOperationException("Arithmetic review requires checked equations or an explicit no-arithmetic judgment.");
        foreach(var item in report.Arithmetic)
        {
            var equation=Regex.Match(item.DisplayedEquation,@"^\s*(.*?)\s*=\s*(-?\d+(?:\.\d+)?)\s*$");
            if(string.IsNullOrWhiteSpace(item.ReviewReason)||!equation.Success||!text.Contains(item.DisplayedEquation,StringComparison.Ordinal)
                ||Regex.Replace(equation.Groups[1].Value,@"\s+","")!=Regex.Replace(item.Expression,@"\s+","")
                ||decimal.Parse(equation.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture)!=item.ExpectedValue||Compute(item.Expression)!=item.ExpectedValue)
                throw new InvalidOperationException("Article arithmetic failed its explicit equation check.");
        }
        var urls=Regex.Matches(text,@"https?://[^\s\)\]>\""']+").Select(m=>m.Value).Concat(requiredSources).Distinct(StringComparer.Ordinal);
        foreach(var url in urls)
        {
            var link=report.Links.SingleOrDefault(l=>l.Url==url);
            if(link is null||link.HttpStatus<200||link.HttpStatus>=300||link.CheckedUtc<report.ReviewedUtc.AddMinutes(-30)||link.CheckedUtc>DateTime.UtcNow.AddMinutes(5)
                ||!Uri.TryCreate(link.FinalUrl,UriKind.Absolute,out var final)||final.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("A final article link lacks fresh successful reachability evidence: "+url);
        }
    }
    public static string VerificationText(GeneratedLongformArticle article)=>string.Join("\n",new[]{article.Title,article.Summary,article.MetaDescription}.Concat(article.IntroParagraphs).Concat(article.Sections.SelectMany(s=>s.Paragraphs)).Concat(article.ConclusionParagraphs).Append(article.CallToAction));
    private static decimal Compute(string expression)
    {
        // Deliberately bounded: unsupported equations require an explicit verified implementation, never a guessed pass.
        if(!Regex.IsMatch(expression,@"^\s*-?\d+(?:\.\d+)?(?:\s*[+\-]\s*\d+(?:\.\d+)?)*\s*$"))throw new InvalidOperationException("Unsupported arithmetic expression.");
        decimal total=0;foreach(Match term in Regex.Matches(Regex.Replace(expression,@"\s+",""),@"[+\-]?\d+(?:\.\d+)?"))total+=decimal.Parse(term.Value,System.Globalization.CultureInfo.InvariantCulture);return total;
    }
}
