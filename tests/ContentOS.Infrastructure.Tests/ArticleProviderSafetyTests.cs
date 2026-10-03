using ContentOS.Application.Abstractions;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using NSubstitute;
using NUnit.Framework;
using System.Net;
using System.Text;
namespace ContentOS.Infrastructure.Tests;
public class ArticleProviderSafetyTests
{
    [TestCase("127.0.0.1")][TestCase("10.0.0.1")][TestCase("192.168.1.2")][TestCase("169.254.169.254")][TestCase("100.64.0.1")][TestCase("203.0.113.4")][TestCase("::1")][TestCase("fc00::1")][TestCase("2001:db8::1")][TestCase("2002:7f00:1::1")][TestCase("::ffff:10.0.0.1")]
    public void NonpublicDestinationsAreRejectedWithoutNetworkUse(string address)=>Assert.That(ArticlePublicPageReader.IsPublicAddress(IPAddress.Parse(address)),Is.False);
    [TestCase("http://localhost/a")][TestCase("https://user:password@example.org/a")][TestCase("file:///C:/test")][TestCase("http://example.org:8080/a")][TestCase("http://10.0.0.1/a")]
    public void UnsupportedUrlsAreRejectedBeforeDnsOrHttp(string url)=>Assert.Throws<InvalidOperationException>(()=>ArticlePublicPageReader.ValidateUrl(url));
    [TestCase("8.8.8.8")][TestCase("2606:4700:4700::1111")]
    public void OrdinaryPublicUnicastAddressesPassAddressClassification(string address)=>Assert.That(ArticlePublicPageReader.IsPublicAddress(IPAddress.Parse(address)),Is.True);
    private static GeneratedLongformArticle Article()=>new("Bill timing","bill-timing","Keep bills ready","Keep bills ready",300,4000,100,1,["Read the [source](https://example.org/source)."],[new("Bills",["Keep essential bill money available."])],["Review at payday."],"Check your dates.",new string('x',60000),new string('y',60000),false,true);
    [TestCase("supported")][TestCase("inventedExcerpt")][TestCase("brokenSource")]
    public async Task SourceReviewUsesActualCollectedQuotesAndCompactStructuredArticle(string scenario)
    {
        const string url="https://example.org/source";const string text="Keep essential bill money available.";
        var reader=Substitute.For<IArticlePublicPageReader>();reader.ReadAsync(url,Arg.Any<CancellationToken>()).Returns(new CollectedArticlePage(url,url,scenario=="brokenSource"?404:200,text,DateTime.UtcNow,FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(text)),"FixturePageCollector",true));
        var model=Substitute.For<ILlmClient>();string? captured=null;
        model.GenerateAsync<ArticleClaimReviewResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>
        {captured=call.ArgAt<string>(0);return Task.FromResult<ArticleClaimReviewResponse?>(new(){CompleteMaterialClaimInventory=true,Claims=[new(text,url,scenario=="inventedExcerpt"?"Invented source quote":text,"The exact supplied excerpt supports this reserve advice.",true)],NoArithmeticReason="No displayed equations occur in this fixture."});});
        var reviewer=new SourceBoundArticleEvidenceReviewer(model,reader);
        if(scenario!="supported"){Assert.ThrowsAsync<InvalidOperationException>(()=>reviewer.ReviewAsync(Article(),[url],default));return;}
        var report=await reviewer.ReviewAsync(Article(),[url],default);ArticlePostEditVerificationService.Validate(Article(),[url],report,true);
        Assert.That(captured,Does.Not.Contain("BodyText").And.Not.Contain("FullText"));Assert.That(report.IsFixture,Is.True);
        Assert.That(report.CollectedSources![0].ProvidedExcerpt,Is.EqualTo(text));Assert.That(report.ReviewKind,Does.Contain("not independent"));
    }
}
