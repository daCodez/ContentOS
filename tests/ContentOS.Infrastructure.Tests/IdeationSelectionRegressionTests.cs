using ContentOS.Application.Research;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;

namespace ContentOS.Infrastructure.Tests;

public sealed class IdeationSelectionRegressionTests
{
    [Test]
    public async Task HistoricalHipResponseRetainsTenDistinctSupportedIdeasWithoutAnotherModelCall()
    {
        using var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","HipIdeationSelectionReplay.json")));
        var context=fixture.RootElement.GetProperty("Context").Deserialize<ResearchContext>()!;
        var findings=fixture.RootElement.GetProperty("Findings").Deserialize<List<ResearchFinding>>()!;
        var response=fixture.RootElement.GetProperty("Response").Deserialize<IdeationResponse>()!;
        var writer=Substitute.For<IWorkflowArticleWriter>();
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(response);
        var agent=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),new IdeaDeduplicatorService(),NullLogger<IdeationAgent>.Instance);
        var actual=await agent.GenerateIdeasAsync(context,findings);
        Assert.That(actual.Select(i=>i.Title),Is.EquivalentTo(response.Ideas.Select(i=>i.Title)));
        Assert.That(actual,Has.Count.EqualTo(10));
        Assert.That(actual.Single(i=>i.Title.StartsWith("The Padlock")).EditorialQualityScore,Is.Null);
        Assert.That(actual.Single(i=>i.Title.StartsWith("When a Trust Score")).EditorialQualityScore,Is.EqualTo(100m));
        Assert.That(actual.All(i=>i.SupportingFindings.Count>0),Is.True);
        Assert.That(context.IdeaSelectionDecisions,Has.Count.EqualTo(10));
        Assert.That(context.IdeaSelectionDecisions.All(d=>d.Retained && d.MatchedSourceUrls.Count>0),Is.True);
        Assert.That(JsonSerializer.Serialize(context),Does.Contain("IdeaSelectionDecisions").And.Contain("RetainedWithSelectedCollectedEvidence"));
        Assert.That(writer.ReceivedCalls().Count(),Is.EqualTo(1)); // mocked replay only
    }

    [Test]
    public async Task SharedGenericAngleAndContentTypeDoNotDiscardDistinctReaderProblems()
    {
        const string url="https://example.org/collected";
        string[] topics=["Read a domain before login","Interpret incomplete evidence","Separate certificates from honesty","Inspect pressure before payment","Understand scan dates","Review privacy boundaries"];
        var response=new IdeationResponse{Ideas=topics.Select((topic,index)=>new IdeationDto{Title=topic,PrimaryKeyword=topic.ToLowerInvariant(),AudiencePainPoint="Distinct reader problem "+topic,AudienceGoal="Understand "+topic,SearchIntent="informational",RecommendedAngle="practical",SupportingSourceUrls=[url]}).ToList()};
        var writer=Substitute.For<IWorkflowArticleWriter>();writer.GenerateIdeationResponseAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(response);
        var agent=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),new IdeaDeduplicatorService(),NullLogger<IdeationAgent>.Instance);
        var actual=await agent.GenerateIdeasAsync(new ResearchContext{MaxIdeasToSave=25},[new(){SourceTitle="Public guidance",SourceUrl=url,SourceExcerpt="An excerpt supporting a public guidance discussion."}]);
        Assert.That(actual,Has.Count.EqualTo(topics.Length));
    }

    [Test]
    public async Task UnsupportedIncompleteDuplicateAndSourceCopiedCandidatesHaveExplicitRejectionEvidence()
    {
        const string url="https://example.org/collected";
        IdeationDto Idea(string title,string? keyword=null)=>new(){Title=title,PrimaryKeyword=keyword??title.ToLowerInvariant(),AudiencePainPoint="The scan timing is unclear",AudienceGoal="Interpret the scan date",SearchIntent="informational",RecommendedAngle="practical",SupportingSourceUrls=[url]};
        var valid=Idea("Interpret a dated assessment");
        var unsupported=Idea("Review a website owner identity");unsupported.SupportingSourceUrls=["https://example.org/invented"];
        var incomplete=Idea("Check a domain before entering a password");incomplete.AudienceGoal="";
        var response=new IdeationResponse{Ideas=[valid,Idea(valid.Title),unsupported,incomplete,Idea("Public reference about secure connections"),Idea("Pause before sending payment","https://example.org/keyword"),Idea("https://example.org/title","empty cleaned title")]};
        var writer=Substitute.For<IWorkflowArticleWriter>();writer.GenerateIdeationResponseAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(response);
        var context=new ResearchContext();var agent=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),new IdeaDeduplicatorService(),NullLogger<IdeationAgent>.Instance);
        var actual=await agent.GenerateIdeasAsync(context,[new(){SourceTitle="Public reference about secure connections",SourceUrl=url,SourceExcerpt="The collected excerpt records connection-related observations."}]);
        Assert.That(actual,Has.Count.EqualTo(1));
        Assert.That(context.IdeaSelectionDecisions,Has.Count.EqualTo(response.Ideas.Count));
        Assert.That(context.IdeaSelectionDecisions.Count(d=>d.Retained),Is.EqualTo(1));
        Assert.That(context.IdeaSelectionDecisions.Single(d=>d.SourceIndex==1).MatchedCandidateTitle,Is.EqualTo(valid.Title));
        Assert.That(context.IdeaSelectionDecisions.Single(d=>d.SourceIndex==2).MatchedSourceUrls,Is.Empty);
        Assert.That(context.IdeaSelectionDecisions.Where(d=>!d.Retained).Select(d=>d.Reason),Is.EquivalentTo(new[]{"DuplicateCanonicalTopicAngleIntentPainOrNearIdenticalTitle","NoSelectedCollectedEvidence","MissingRequiredReaderOrIdeaFields","NearDuplicateOfCollectedSourceHeadline","KeywordExceedsBoundOrContainsUrl","EmptyIdeaFieldsAfterCleaning"}));
    }
}
