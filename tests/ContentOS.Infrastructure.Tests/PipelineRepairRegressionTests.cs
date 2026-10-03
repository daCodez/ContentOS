using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ContentOS.Application.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;
public class PipelineRepairRegressionTests
{
    [Test]
    public async Task AssessmentCannotClaimThatItRewroteProse()
    {
        var report = await new EditorialAgent().HumanizeAndImproveReadabilityAsync(Article());
        Assert.That(string.Join(" ", report.Notes), Does.Not.Contain("Shortened sentences"));
    }

    [Test]
    public async Task PrewriteBriefUsesPlannedLengthInsteadOfZeroWords()
    {
        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("Irregular income budget");
        var agent = new ContentStrategyAgent(NullLogger<ContentStrategyAgent>.Instance, llm, Options.Create(new SimulationSettings()));
        var brief = await agent.BuildContentBriefAsync(new ContentIdea { Title="Irregular income budget", PrimaryKeyword="irregular income budget", Summary="Plan due dates", SearchIntent="Informational", AudiencePainPoint="Pay changes", AudienceGoal="Pay essentials", RecommendedAngle="Practical", WhyNow="Variable pay" }, Article() with { EstimatedWordCount=0 }, ["variable earnings"], ["reviewed source"]);
        Assert.That(string.Join(" ",brief.Brief), Does.Not.Match(@"(?:^|\s)0 words\b"));
        Assert.That(brief.EstimatedWordCount, Is.GreaterThanOrEqualTo(1800));
    }

    [Test]
    public async Task MonetizationDoesNotInventDownloadsOrFamilySpecificOffers()
    {
        var report = await new SeoAndMonetizationAgent(NullLogger<SeoAndMonetizationAgent>.Instance).AddCtaAndMonetizationPlacementsAsync(Article());
        Assert.That(report.LeadMagnetCta, Is.EqualTo(Article().CallToAction));
        Assert.That(string.Join(" ", report.ToolRecommendations), Does.Not.Contain("family"));
        Assert.That(string.Join(" ", report.ToolRecommendations), Does.Not.Contain("lead magnet"));
    }

    internal static GeneratedLongformArticle Article() => new("Irregular income budget","irregular-income-budget","Plan due dates","Manage variable earnings",1800,2600,1900,8,
        ["Pay changes.","Bills arrive.","Start with available cash."],
        [new("What You'll Learn",["- Plan dates."]),new("Worked Example",["$300 + $700 - $750 = $250. See [CFPB](https://consumerfinance.gov/example)."]),new("Tools",["A spreadsheet tracks due dates."]),new("FAQ",["What if pay varies? Update expected receipts."])],
        ["Check your plan weekly."],"Copy the table into your plan.","","",false,true);
}
