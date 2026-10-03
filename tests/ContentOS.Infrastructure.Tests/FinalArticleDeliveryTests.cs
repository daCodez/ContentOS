using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using ContentOS.Infrastructure.Writing;
using NUnit.Framework;
using System.Text;

namespace ContentOS.Infrastructure.Tests;

public class FinalArticleDeliveryTests
{
    private string _root="";
    [SetUp] public void Setup(){_root=Path.Combine(Path.GetTempPath(),"contentos-delivery-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_root);}
    [TearDown] public void Cleanup(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
    private static GeneratedLongformArticle Article()=>new("A useful fixture", "useful-fixture", "Plan bill timing", "Plan cash around bills", 100,500,90,1,
        ["Use the [source](https://example.org/source) to compare your cash-flow dates."],
        [new("Next step",["- [ ] List bills\n- [ ] Check pay dates", "| Bill | Due |\n| --- | --- |\n| Rent | Friday |"]),new("Next step",["Set aside your essential bill money before spending extra pay."]),new("FAQ",["### What if pay arrives late?\nCall the provider before the due date and ask about your options."])],
        ["Review the plan each payday."],"Use the checklist above for your next payday.","fixture body","fixture body",false,true);
    private ArticleDeliveryImage Image(GeneratedLongformArticle article)
    {
        var path=Path.Combine(_root,"cash-flow.svg");File.WriteAllText(path,"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"640\" height=\"200\"><rect width=\"640\" height=\"200\" fill=\"white\"/><text x=\"20\" y=\"50\">List bills, then check pay dates</text></svg>");
        return new(path,FinalArticleDelivery.Hash(File.ReadAllBytes(path)),"List bills before checking pay dates","OfflineFixture",EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article)),true);
    }
    [Test]
    public async Task HypotheticalExplorerUsesNativeAccessibleRevealsAndRetainsStaticText()
    {
        var article=Article() with{Sections=[..Article().Sections,new("Hypothetical evidence explorer",["Hypothetical example, not a live website scan.","Clue: The connection is encrypted. That observation alone does not establish operator honesty.","Coverage: Evidence is missing. Keep uncertainty visible."])]};
        var package=await FinalArticleDelivery.BuildAsync(article,[Image(article)],_root,["https://example.org/source"],default);
        Assert.That(package.Html,Does.Contain("<details>").And.Contain("<summary>Reveal clue 1</summary>").And.Not.Contain("<script"));
        Assert.That(package.Markdown,Does.Contain("Clue: The connection is encrypted.").And.Contain("Coverage: Evidence is missing."));
    }
    [Test]
    public async Task ExactExportsRetainClickableUniqueTocTablesChecklistsSourcesAndRealInsertedImage()
    {
        var article=Article();var package=await FinalArticleDelivery.BuildAsync(article,[Image(article)],_root,["https://example.org/source"],default);
        Assert.That(package.Html,Does.Contain("<table>").And.Contain("type=\"checkbox\"").And.Contain("data:image/svg+xml;base64,").And.Contain("href=\"https://example.org/source\""));
        Assert.That(package.Markdown,Does.Contain("data:image/svg+xml;base64,").And.Contain("- [ ]").And.Contain("| Rent | Friday |"));
        Assert.That(package.Toc.Select(t=>t.Anchor).Distinct().Count(),Is.EqualTo(package.Toc.Count));
        Assert.That(package.Toc.All(t=>package.Html.Contains("id=\""+t.Anchor+"\"")),Is.True);
        Assert.That(package.HtmlHash,Is.EqualTo(FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(package.Html))));
        Assert.That(FinalArticleDelivery.CanComplete(package,100,package.PackageHash,"Eric",package.PackageHash),Is.False,"Fixture assets cannot satisfy production approval.");
    }
    [TestCase("missingFile")]
    [TestCase("wrongHash")]
    [TestCase("missingAlt")]
    [TestCase("staleVersion")]
    [TestCase("placeholder")]
    public void MissingOrBrokenAssetsBlockRegardlessOfScore(string defect)
    {
        var article=Article();var image=Image(article);
        image=defect switch
        {
            "missingFile"=>image with{LocalPath=Path.Combine(_root,"missing.svg")},"wrongHash"=>image with{Sha256="wrong"},
            "missingAlt"=>image with{AltText=""},"staleVersion"=>image with{ArticleVersionHash="old-version"},"placeholder"=>image with{Provider="Placeholder"},_=>image
        };
        Assert.ThrowsAsync<InvalidOperationException>(()=>FinalArticleDelivery.BuildAsync(article,[image],_root,["https://example.org/source"],default));
    }
    [Test]
    public void MissingRequiredSourceOrImageIsAHardBlocker()
    {
        var article=Article();Assert.ThrowsAsync<InvalidOperationException>(()=>FinalArticleDelivery.BuildAsync(article,[],_root,["https://example.org/source"],default));
        Assert.ThrowsAsync<InvalidOperationException>(()=>FinalArticleDelivery.BuildAsync(article,[Image(article)],_root,["https://missing.org/source"],default));
    }
    [Test]
    public void FabricatedPngHeaderCannotSatisfyImageReadiness()
    {
        var article=Article();var original=Image(article);var path=Path.Combine(_root,"broken.png");var bytes=new byte[32];
        new byte[]{137,80,78,71,13,10,26,10}.CopyTo(bytes,0);bytes[19]=64;bytes[23]=64;File.WriteAllBytes(path,bytes);
        var image=original with{LocalPath=path,Sha256=FinalArticleDelivery.Hash(bytes)};
        Assert.ThrowsAsync<InvalidOperationException>(()=>FinalArticleDelivery.BuildAsync(article,[image],_root,["https://example.org/source"],default));
    }
    [Test]
    public void RepeatedFillerAndMetaWritingCannotProduceACompletedDeliveryPackage()
    {
        var article=Article();var paragraph="A useful longform article also benefits from grounded evidence and should include detailed examples for readers.";
        article=article with{Sections=[new("First",[paragraph]),new("Second",[paragraph])]};
        Assert.ThrowsAsync<InvalidOperationException>(()=>FinalArticleDelivery.BuildAsync(article,[Image(article)],_root,["https://example.org/source"],default));
    }
}
