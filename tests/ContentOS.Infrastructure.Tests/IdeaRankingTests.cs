using ContentOS.Application.Research;
using ContentOS.Infrastructure.Scoring;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;
public class IdeaRankingTests
{
    private static IdeaReviewResponse Review(decimal rating=3) => new() { Dimensions = IdeaRankingPolicy.DefaultWeights.Keys.Select(k => new IdeaReviewDimension { Key=k, Rating = k is "demand" or "competition" or "trend" ? null : rating, Reason="The specific reader decision is supported by the supplied excerpt; market measurements are unavailable.", Evidence=[new("https://example.org/source", "The reader needs clear reasons before trusting a website.")] }).ToList() };
    private static readonly Dictionary<string,string> Sources=new(){["https://example.org/source"]="The reader needs clear reasons before trusting a website."};
    [Test] public void UnknownMarketEvidenceLeavesNullableOverallAndAnExplicitRange()
    {
        var result=IdeaRankingEvaluator.Evaluate(Review(),new(),Sources);
        Assert.That(result.Status,Is.EqualTo("Provisional"));Assert.That(result.OverallScore,Is.Null);
        Assert.That(result.KnownWeight,Is.EqualTo(70));Assert.That(result.LowerBound,Is.EqualTo(52.5m));Assert.That(result.UpperBound,Is.EqualTo(82.5m));
        Assert.That(result.Dimensions.Single(x=>x.Key=="demand").Rating,Is.Null);
    }
    [Test] public void InvalidCitationOrParaphrasedQuoteRejectsReview()
    {var review=Review();review.Dimensions[0].Evidence=[new("https://invented.org", "The reader needs clear reasons before trusting a website.")];Assert.That(IdeaRankingEvaluator.Evaluate(review,new(),Sources).Status,Is.EqualTo("InvalidReview"));review=Review();review.Dimensions[0].Evidence=[new("https://example.org/source","Readers need reasons.")];Assert.That(IdeaRankingEvaluator.Evaluate(review,new(),Sources).Status,Is.EqualTo("InvalidReview"));}
    [Test] public void ReviewerCannotClaimMeasuredDemandFromAnOrdinaryExcerpt()
    {var review=Review();review.Dimensions.Single(x=>x.Key=="demand").Rating=4;Assert.That(IdeaRankingEvaluator.Evaluate(review,new(),Sources).Status,Is.EqualTo("InvalidReview"));}
    [Test] public void MissingReviewIsUnassessedNotGeneratorScore()
    {Assert.That(IdeaRankingEvaluator.Evaluate(null,new(),Sources).OverallScore,Is.Null);Assert.That(IdeaRankingEvaluator.Evaluate(null,new(),Sources).Status,Is.EqualTo("Unassessed"));}
    [Test] public void WeightsMustMatchDimensionsAndTotalOneHundred()
    {var policy=new IdeaRankingPolicy();policy.Weights["usefulness"]=99;Assert.Throws<ArgumentException>(()=>IdeaRankingEvaluator.Evaluate(Review(),policy,Sources));}
    [Test] public void StrongerEvidenceBoundLowerScoreRanksAboveWeakReviewWithoutPretendingCertainty()
    {var strong=IdeaRankingEvaluator.Evaluate(Review(4),new(),Sources);var weak=IdeaRankingEvaluator.Evaluate(Review(1),new(),Sources);Assert.That(strong.LowerBound,Is.GreaterThan(weak.UpperBound!.Value));Assert.That(strong.OverallScore,Is.Null);}
    [Test] public void PerfectEditorialJudgmentIsAllowedButCannotProduceConfidentHundredWithUnknownSeo()
    {var result=IdeaRankingEvaluator.Evaluate(Review(4),new(),Sources);Assert.That(result.LowerBound,Is.EqualTo(70));Assert.That(result.UpperBound,Is.EqualTo(100));Assert.That(result.Status,Is.EqualTo("Provisional"));}
    [TestCase(null)][TestCase("javascript:alert(1)")]
    public void UnsafeOrMissingCitationUrlIsInvalidInsteadOfThrowing(string? url)
    {var review=Review();review.Dimensions[0].Evidence=[new(url!,Sources.Values.Single())];var sources=new Dictionary<string,string>(Sources);if(url is not null)sources[url]=Sources.Values.Single();Assert.That(IdeaRankingEvaluator.Evaluate(review,new(),sources).Status,Is.EqualTo("InvalidReview"));}
    [Test] public void ConfigurableWeightsChangeEvidenceContributionWithoutRenormalizingUnknowns()
    {var policy=new IdeaRankingPolicy();policy.Weights["demand"]=25;policy.Weights["usefulness"]=5;var result=IdeaRankingEvaluator.Evaluate(Review(),policy,Sources);Assert.That(result.KnownWeight,Is.EqualTo(60));Assert.That(result.LowerBound,Is.EqualTo(45));Assert.That(result.UpperBound,Is.EqualTo(85));}
    [Test] public void NullUnknownEvidenceListIsInvalid()
    {var review=Review();review.Dimensions.Single(d=>d.Key=="demand").Evidence=null!;Assert.That(IdeaRankingEvaluator.Evaluate(review,new(),Sources).Status,Is.EqualTo("InvalidReview"));}
    [Test] public void EvidenceBasedSeoIntentChangesProvisionalRank()
    {var weak=Review();weak.Dimensions.Single(d=>d.Key=="intentFit").Rating=1;var strong=Review();strong.Dimensions.Single(d=>d.Key=="intentFit").Rating=4;Assert.That(IdeaRankingEvaluator.Evaluate(strong,new(),Sources).LowerBound-IdeaRankingEvaluator.Evaluate(weak,new(),Sources).LowerBound,Is.EqualTo(7.5m));}
    [Test] public async Task OversizedReviewIsBlockedBeforeTransport()
    {
        var llm=NSubstitute.Substitute.For<ContentOS.Application.Abstractions.ILlmClient>();var reviewer=new SeparateIdeaReviewer(llm);
        var idea=new CandidateContentIdea{Title=new string('x',45000)};
        Assert.ThrowsAsync<InvalidOperationException>(async()=>await reviewer.ReviewAsync(idea,new(),[],new(),default));
        Assert.That(NSubstitute.SubstituteExtensions.ReceivedCalls(llm),Is.Empty);
        await Task.CompletedTask;
    }
}
