using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;
namespace ContentOS.Infrastructure.Tests;
public class ArticlePostEditWorkflowTests
{
    private const string Url="https://example.org/source";
    private static GeneratedLongformArticle Article()=>new("Payday planning","payday-planning","Plan bills","Plan bills",300,4000,100,1,
        ["Plan bill timing using the [source](https://example.org/source)."],[new("Bill plan",["Your reserve calculation is 100+20 = 120. Keep essential bill money available."])],
        ["Check the plan each payday."],"Use your bill calendar.","original","original",false,true);
    private static ArticleVerificationReport Report(GeneratedLongformArticle article)=>new(EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article)),"OfflineFixtureReviewer","ExplicitSourceReview",DateTime.UtcNow,true,
        [new("Keep essential bill money available.",Url,"Set aside essential bill money.","The supplied excerpt supports this bill reserve advice.",true)],
        [new("100+20",120,"Checked the stated reserve addition.","100+20 = 120")],null,[new(Url,200,DateTime.UtcNow,Url)],[],true);
    [Test]
    public async Task PostEditReviewRetainsExactClaimsArithmeticAndFreshLinkRecords()
    {
        var article=Article();var reviewer=Substitute.For<IArticleEvidenceReviewer>();var report=Report(article);
        reviewer.ReviewAsync(article,Arg.Any<IReadOnlyList<string>>(),Arg.Any<CancellationToken>()).Returns(report);
        var actual=await new ArticlePostEditVerificationService(reviewer).VerifyAsync(article,[Url],true,default);
        Assert.That(actual,Is.SameAs(report));Assert.That(actual.ArticleVersionHash,Is.EqualTo(EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article))));
    }
    [TestCase("staleVersion")][TestCase("unresolved")][TestCase("incompleteClaims")][TestCase("inventedClaim")][TestCase("badArithmetic")][TestCase("brokenLink")][TestCase("oldLink")][TestCase("fixtureProduction")]
    public void UnverifiedPostEditEvidenceBlocksDelivery(string defect)
    {
        var article=Article();var report=Report(article);
        report=defect switch
        {
            "staleVersion"=>report with{ArticleVersionHash="old"},"unresolved"=>report with{UnresolvedFailures=["Unsupported claim"]},
            "incompleteClaims"=>report with{CompleteMaterialClaimInventory=false},"inventedClaim"=>report with{Claims=[report.Claims[0] with{Text="Invented text"}]},
            "badArithmetic"=>report with{Arithmetic=[new("100+20",121,"Wrong result")]},"brokenLink"=>report with{Links=[new(Url,404,DateTime.UtcNow,Url)]},
            "oldLink"=>report with{Links=[new(Url,200,DateTime.UtcNow.AddDays(-1),Url)]},_=>report
        };
        Assert.Throws<InvalidOperationException>(()=>ArticlePostEditVerificationService.Validate(article,[Url],report,defect!="fixtureProduction"));
    }
    private static FullWorkflowStepContext Context(string key,ArticleDeliveryWorkflowState state)=>new(Guid.NewGuid(),Guid.NewGuid(),
        new(Guid.NewGuid(),WorkflowDefinitionType.Article,"Article fixture",1,JsonSerializer.SerializeToElement(new{ScoringContract=new{}}),[]),
        new(Guid.NewGuid(),key,key,1,"Use plain concrete language.","Actual changed article",[]),JsonSerializer.SerializeToElement(new{}),JsonSerializer.SerializeToElement(state),"fixture",1,true);
    [Test]
    public async Task ActualEditorialStepInvalidatesAllEarlierVerificationScoresAndMedia()
    {
        var article=Article();var writer=Substitute.For<IWorkflowArticleWriter>();var revised=EditorialRevisionService.ToDraft(article);
        revised.IntroParagraphs=["Use the [source](https://example.org/source) to plan when bills fall due."];
        writer.ReviseArticleAsync(Arg.Any<EditorialRevisionRequest>(),Arg.Any<CancellationToken>()).Returns(revised);
        var state=new ArticleDeliveryWorkflowState{Article=article,Tone="Plain and practical",RequiredSources=[Url],PostEditReview=Report(article),ScoredPackageHash="old",Images=[new("old.png","old","old","old","old")]};
        var result=await new ArticleDeliveryWorkflowStepHandler("HumanizeArticle",writer).ExecuteAsync(Context("HumanizeArticle",state),default);
        var actual=result.Output.Deserialize<ArticleDeliveryWorkflowState>()!;
        Assert.That(actual.Article!.IntroParagraphs[0],Is.EqualTo(revised.IntroParagraphs[0]));Assert.That(actual.PostEditReview,Is.Null);
        Assert.That(actual.Images,Is.Empty);Assert.That(actual.ScoredPackageHash,Is.Null);Assert.That(actual.ContentVersionHistory.Distinct().Count(),Is.EqualTo(2));Assert.That(actual.AwaitingHumanApproval,Is.True);
    }
    [Test] public void CorrectExpressionCannotHideAnIncorrectPrintedAnswer()
    {
        var article=Article();var report=Report(article);article=article with{Sections=[new("Bill plan",["Your reserve calculation is 100+20 = 121. Keep essential bill money available."])]};
        report=report with{ArticleVersionHash=EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article)),Arithmetic=[new("100+20",120,"Correct sum but wrong printed result","100+20 = 121")]};
        Assert.Throws<InvalidOperationException>(()=>ArticlePostEditVerificationService.Validate(article,[Url],report,true));
    }
    [TestCase(101)][TestCase(84)][TestCase(100)] public void OutOfRangeOrUnsupportedEditorialScoresCannotComplete(decimal score)
    {
        var assessment=new ContentOS.Application.Research.EditorialAssessmentResult("AssessedModelOpinion",score,"article-editorial-proposal-v1",[],[]);
        Assert.Throws<InvalidOperationException>(()=>ArticleDeliveryWorkflowStepHandler.ValidateEditorialAssessment(assessment,ContentOS.Application.Research.EditorialRubric.ArticleProposal,[Url]));
    }
    [TestCase("CreateArticleImages")][TestCase("RecheckPostEditEvidence")]
    public void MissingAuthorizedProvidersBlockInsteadOfProducingPlaceholderSuccess(string key)
    {
        var state=new ArticleDeliveryWorkflowState{Article=Article(),Tone="Plain",RequiredSources=[Url]};
        Assert.ThrowsAsync<InvalidOperationException>(()=>new ArticleDeliveryWorkflowStepHandler(key,Substitute.For<IWorkflowArticleWriter>()).ExecuteAsync(Context(key,state),default));
    }
}
