using ContentOS.Application.Research;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;

namespace ContentOS.Infrastructure.Tests;

public class IdeationScoringExecutionTests
{
    [TestCase("missingRating")]
    [TestCase("nullDimension")]
    public async Task MalformedModelAssessmentRemainsInvalidInActualIdeaPath(string defect)
    {
        const string url = "https://example.org/collected";
        var dimensions = EditorialRubric.IdeaProposal.Dimensions.Select(d => new Dictionary<string, object?>
        { ["key"] = d.Key, ["rating"] = 3, ["reason"] = "Specific evidence supports this editorial judgment.", ["evidenceReferences"] = new[] { url } }).Cast<object?>().ToList();
        if (defect == "missingRating") ((Dictionary<string, object?>)dimensions[0]!).Remove("rating");
        else dimensions[0] = null;
        var json = JsonSerializer.Serialize(new { ideas = new[] { new { title = "Build a lean-month reserve before spending extra pay", primaryKeyword = "irregular income budget", audiencePainPoint="Variable pay arrives after bills",audienceGoal="Cover essentials",searchIntent="Informational",recommendedAngle = "practical", supportingSourceUrls = new[] { url }, editorialAssessment = new { rubricVersion = EditorialRubric.IdeaProposal.Version, dimensions } } } });
        var response = JsonSerializer.Deserialize<IdeationResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        var agent = new IdeationAgent(writer, Substitute.For<ITopicDiversityScorer>(), new IdeaDeduplicatorService(), NullLogger<IdeationAgent>.Instance);
        var ideas = await agent.GenerateIdeasAsync(new ResearchContext(), [new() { SourceTitle = "Cash flow example", SourceUrl = url, SourceExcerpt = "$2100 income covers $1850 essentials.", PainPoint = "Variable pay" }]);
        Assert.That(ideas.Single().EditorialScoringStatus, Is.EqualTo("InvalidAssessment"));
        Assert.That(ideas.Single().EditorialQualityScore, Is.Null);
    }

    [TestCase(null, null, "Unassessed")]
    [TestCase(0, 0, "AssessedModelOpinion")]
    [TestCase(4, 100, "AssessedModelOpinion")]
    public async Task ActualIdeationPathAggregatesExplicitJudgmentsWithoutLegacyHeuristics(int? rating, int? expected, string status)
    {
        const string url = "https://example.org/collected";
        var writer = Substitute.For<IWorkflowArticleWriter>();
        var legacy = Substitute.For<ITopicDiversityScorer>();
        var assessment = rating is null ? null : new EditorialAssessmentResponse
        {
            RubricVersion = EditorialRubric.IdeaProposal.Version,
            Dimensions = EditorialRubric.IdeaProposal.Dimensions.Select(d => new EditorialDimensionAssessment
            { Key = d.Key, Rating = rating.Value, Reason = "This judgment relates to the supplied lean-month reserve example.", EvidenceReferences = [url] }).ToList()
        };
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdeationResponse?>(new()
        {
            Ideas = [new() { Title = "Build a lean-month reserve before spending extra pay", PrimaryKeyword = "irregular income budget", AudiencePainPoint="Variable pay arrives after bills",AudienceGoal="Cover essentials",SearchIntent="Informational",RecommendedAngle = "practical", SupportingSourceUrls = [url], EditorialAssessment = assessment }]
        }));
        var agent = new IdeationAgent(writer, legacy, new IdeaDeduplicatorService(), NullLogger<IdeationAgent>.Instance);
        var ideas = await agent.GenerateIdeasAsync(new ResearchContext(), [new() { SourceTitle = "Cash flow example", SourceUrl = url, SourceExcerpt = "$2100 income covers $1850 essentials.", PainPoint = "Variable pay" }]);
        var idea = ideas.Single();
        Assert.That(idea.EditorialQualityScore, Is.EqualTo(expected is null ? null : (decimal?)expected.Value));
        Assert.That(idea.EditorialScoringStatus, Is.EqualTo(status));
        Assert.That(idea.SeoMeasurementStatus, Is.EqualTo("Unknown"));
        Assert.That(legacy.ReceivedCalls(), Is.Empty);
        var prompt = (string)writer.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.That(prompt, Does.Contain("idea-editorial-proposal-v1").And.Contain("GeneratorSelfAssessment").And.Contain("editorialAssessment"));
    }
    [TestCase("noIdeas")][TestCase("missingReaderProblem")]
    public async Task EmptyOrIncompleteModelIdeasAreNotFilledWithInventedReaderProblems(string defect)
    {
        var writer=Substitute.For<IWorkflowArticleWriter>();var response=new IdeationResponse();
        if(defect=="missingReaderProblem")response.Ideas.Add(new(){Title="Check an unfamiliar website before entering a password",PrimaryKeyword="check website safety",AudienceGoal="Avoid a deceptive website",SearchIntent="informational",RecommendedAngle="practical",SupportingSourceUrls=["https://example.org/source"]});
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(response);
        var agent=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),new IdeaDeduplicatorService(),NullLogger<IdeationAgent>.Instance);
        var ideas=await agent.GenerateIdeasAsync(new ResearchContext{VerifiedProductContext="HIP is website trust, not medical HIP.",ApprovedWorkflowInstructions="Prioritize website visitors and evidence-backed limitations."},[new(){SourceTitle="Website trust guidance",SourceUrl="https://example.org/source",SourceExcerpt="HTTPS encrypts the connection; it does not prove website honesty."}]);
        Assert.That(ideas,Is.Empty);var prompt=(string)writer.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.That(prompt,Does.Contain("HIP is website trust, not medical HIP.").And.Contain("Prioritize website visitors"));
    }
}
