using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;

namespace ContentOS.Infrastructure.Tests;

public class RevisedIdeaWorkflowExecutionTests
{
    [Test]
    public async Task FullNineStepSpecificationExecutesRealServicesAndRetainsPendingApprovalEvidence()
    {
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Fixture ideas",WorkflowType=WorkflowDefinitionType.Idea};
        var site=new Site{Id=Guid.NewGuid(),Name="Personal publishing fixture",Domain="https://example.org",Niche="Variable income",DefaultTone="Calm and direct"};
        db.WorkflowDefinitionFamilies.Add(family);db.Sites.Add(site);await db.SaveChangesAsync();
        var source=new ResearchFinding{SourceUrl="https://example.org/collected",SourceTitle="Cash flow example",SourceType="FixtureEvidence",SourceExcerpt="When variable pay arrives after bill due dates, a lean-month reserve helps cover essential bills.",PainPoint="Variable pay arrives after bills"};
        var provider=Substitute.For<IResearchSourceProvider>();provider.ResearchAsync(Arg.Any<ResearchContext>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<ResearchFinding>>([source]));
        var writer=Substitute.For<IWorkflowArticleWriter>();writer.GenerateIdeationResponseAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdeationResponse?>(new()
        {
            Ideas=[new(){Title="Build a lean-month reserve before spending extra pay",PrimaryKeyword="irregular income budget",AudiencePainPoint="Variable pay arrives after bills",AudienceGoal="Cover essentials",SearchIntent="informational",RecommendedAngle="practical",SupportingSourceUrls=[source.SourceUrl],SecondaryKeywords=["variable pay bill calendar"],EditorialAssessment=new()
            {RubricVersion=EditorialRubric.IdeaProposal.Version,Dimensions=EditorialRubric.IdeaProposal.Dimensions.Select(d=>new EditorialDimensionAssessment{Key=d.Key,Rating=3,Reason="Specific reserve planning helps the supplied reader problem.",EvidenceReferences=[source.SourceUrl]}).ToList()}}]
        }));
        var search=Substitute.For<IResearchSearchClient>();search.SearchAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<int>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<SearchResult>>([new(){Title="Actual offline search response",Url=source.SourceUrl,Content=source.SourceExcerpt}]));
        var deduplicator=new IdeaDeduplicatorService();var ideation=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),deduplicator,NullLogger<IdeationAgent>.Instance);
        var handlers=IdeaWorkflowStepHandler.Capabilities.Select(key=>(IFullWorkflowStepHandler)new IdeaWorkflowStepHandler(key,db,[provider],ideation,search,deduplicator)).ToArray();
        var store=new FullWorkflowSpecificationStore(db);
        var json=await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedIdeaWorkflow.json"));
        var definition=await store.ImportDraftAsync(json,family.Id,"Fixture",default);
        var runtime=new FullWorkflowStepRuntime(db,store,handlers);
        var run=await runtime.StartFixtureAsync(definition.Id,JsonSerializer.Serialize(new{siteId=site.Id,audienceDescription="Beginners managing variable pay",seedTopics=new[]{"irregular income budget"},targetIdeaCount=25}),default);
        await runtime.ProcessAsync(run.Id,default);
        Assert.That(run.ErrorMessage,Does.Contain("Fixture"),run.ErrorMessage);
        Assert.That(await db.WorkflowStepRuns.CountAsync(s=>s.Status==WorkflowDefinitionRunStatus.Completed),Is.EqualTo(9));
        Assert.That(await db.ContentArtifacts.CountAsync(),Is.EqualTo(9));
        Assert.That(run.Status,Is.EqualTo(WorkflowDefinitionRunStatus.AwaitingHumanApproval));
        var idea=await db.IdeaRecords.SingleAsync();Assert.That(idea.Status,Is.EqualTo(IdeaRecordStatus.Pending));Assert.That(idea.ApprovedUtc,Is.Null);
        using var snapshot=JsonDocument.Parse(idea.IdeaSnapshotJson);
        Assert.That(snapshot.RootElement.GetProperty("editorialQualityScore").GetDecimal(),Is.EqualTo(75));
        Assert.That(snapshot.RootElement.GetProperty("editorialAssessment").GetProperty("IndependentReview").GetBoolean(),Is.False);
        Assert.That(snapshot.RootElement.GetProperty("measuredSeo").GetProperty("searchVolume").ValueKind,Is.EqualTo(JsonValueKind.Null));
        Assert.That(snapshot.RootElement.GetProperty("audienceType").GetString(),Is.EqualTo("Beginners managing variable pay"));
        var last=await db.WorkflowStepRuns.OrderByDescending(s=>s.CompletedUtc).FirstAsync();using var output=JsonDocument.Parse(last.OutputSnapshotJson!);
        Assert.That(output.RootElement.GetProperty("payload").GetProperty("ShortfallReason").GetString(),Does.Contain("Returned 1 of 25"));
        Assert.That(writer.ReceivedCalls().Count(),Is.EqualTo(1));Assert.That(search.ReceivedCalls().Count(),Is.EqualTo(1));
        Assert.That(await db.WorkflowDefinitionRuns.CountAsync(r=>r.WorkflowType==WorkflowDefinitionType.Article),Is.Zero);
    }
}
