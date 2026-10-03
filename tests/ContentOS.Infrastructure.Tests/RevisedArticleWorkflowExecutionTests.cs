using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using System.Text.Json;
namespace ContentOS.Infrastructure.Tests;

public class RevisedArticleWorkflowExecutionTests
{
    private const string Url="https://example.org/source";
    private const string Table="| Bill | Due |\n| --- | --- |\n| Rent | Friday |";
    private static readonly string Source=JsonSerializer.Serialize(new{url=Url,excerpt="Keep essential bill money available."});
    private static WorkflowArticleDraft Draft()=>new()
    {
        Title="Plan bill timing around variable pay",Slug="bill-timing",ContentType="LongFormBlogArticle",Summary="Plan bill timing around actual cash arrivals.",MetaDescription="Plan bill timing around your actual pay dates.",TargetWordCountMin=300,TargetWordCountMax=600,
        IntroParagraphs=["Plan bill timing using the [source](https://example.org/source) and the practical steps below to prepare before each payday."],
        Sections=[new(){Heading="Bill plan",Paragraphs=["Keep essential bill money available. Record the date each bill falls due and compare it with the date you expect to receive pay. Separate money already committed to bills from money available for other spending. A calendar helps you see a short week before it arrives. Contact the provider before a missed payment when you need to ask about payment dates. Record the answer instead of assuming the due date changed. Keep the plan where you can check it before moving money or agreeing to new spending.",Table]},
            new(){Heading="Reserve example",Paragraphs=["This hypothetical reserve calculation is 100+20 = 120. The example describes amounts already reserved and an additional deposit; it does not promise a saving or return. Your own required reserve depends on bills, cash arrival dates and money already committed. Update the figures when pay changes. Keep the reserve separate from money you can spend freely. Check that the same money has not been assigned to two different bills. A small buffer can help with timing, but this example does not determine what every household needs."]},
            new(){Heading="Checklist",Paragraphs=["- [ ] List bill due dates\n- [ ] Check expected pay dates\n- [ ] Identify committed money\n- [ ] Review your reserve"]},
            new(){Heading="FAQ",Paragraphs=["### What if my pay arrives late?\nCheck which bills fall due before the revised pay date. Contact the provider to ask about available arrangements. Record any agreement and confirm the date before relying on it. Review money already set aside and avoid counting expected income as available cash. If the situation changes again, update the calendar and contact the provider rather than relying on the old plan."]}],
        ConclusionParagraphs=["Review the calendar whenever pay arrives or a bill changes. Keep the plan simple enough to update, and use the current dates rather than an assumed monthly pattern."],CallToAction="Use the checklist to review the bills falling due before your next expected payday."
    };
    private static ArticleVerificationReport Review(GeneratedLongformArticle article)=>new(EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article)),"FixtureReviewer","FixtureSourceBoundReview",DateTime.UtcNow,true,
        [new("Keep essential bill money available.",Url,"Keep essential bill money available.","The supplied fixture source explicitly supports this reserve advice.",true)],
        [new("100+20",120,"Explicit displayed addition checked.","100+20 = 120")],null,System.Text.RegularExpressions.Regex.Matches(ArticlePostEditVerificationService.VerificationText(article),@"https?://[^\s\)\]>\""']+").Select(m=>m.Value).Append(Url).Distinct().Select(url=>new ReviewedArticleLink(url,200,DateTime.UtcNow,url)).ToArray(),[],true);
    [TestCase("success")][TestCase("withPublishedLink")][TestCase("inventedGapQuote")][TestCase("changedDraftTopic")][TestCase("missingInventory")][TestCase("invalidFinalAssessment")]
    public async Task CompleteConfiguredArticleStepsExecuteActualOperationsOrStopAtTheSpecificFailure(string scenario)
    {
        var assetRoot=Path.Combine(Path.GetTempPath(),"contentos-full-article-fixture-"+Guid.NewGuid().ToString("N"));
        try
        {
            await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
            await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
            var family=new WorkflowDefinitionFamily{Id=Guid.NewGuid(),Name="Article fixture",WorkflowType=WorkflowDefinitionType.Article};
            var site=new Site{Id=Guid.NewGuid(),Name="Fixture",Domain="https://example.org",Niche="Budgeting",DefaultTone="Plain and practical"};
            var idea=new IdeaRecord{Id=Guid.NewGuid(),SiteId=site.Id,IdeaTitle="Plan bill timing around variable pay",ReaderProblem="Bills fall due before pay.",AudienceType="Beginning budgeters with variable pay",SearchIntent="informational",UniquenessAngle="cash arrival calendar",Status=IdeaRecordStatus.Approved,ApprovedBy="OfflineFixture",ApprovedUtc=DateTime.UtcNow,
                IdeaSnapshotJson=JsonSerializer.Serialize(new{primaryKeyword="bill timing",audienceGoal="Cover bills before spending extra pay",summary="Plan bill timing",contentType="LongFormBlogArticle",slugSuggestion="bill-timing",secondaryKeywordsJson="[\"pay dates\"]",sourceSummaryJson=JsonSerializer.Serialize(new[]{Source})})};
            db.AddRange(family,site,idea);await db.SaveChangesAsync();
            var model=Substitute.For<ILlmClient>();
            model.GenerateAsync<SourceBoundArticleBriefResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new SourceBoundArticleBriefResponse{WorkingTitle=idea.IdeaTitle,Brief=[idea.ReaderProblem,"Use a bill calendar and explain one hypothetical reserve example."],KeywordCluster=["bill timing","pay dates"],EvidenceSelections=[new(SourceBoundArticleBriefContract.Describe([Source]).Single().SourceId,"Keep essential bill money available.")],EstimatedWordCount=400,IsQualitySufficient=true});
            model.GenerateAsync<ArticleGapResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ArticleGapResponse{Gaps=[new(Url,scenario=="inventedGapQuote"?"Invented quote":"Keep essential bill money available.","Explain reserve timing","This excerpt mentions reserves but does not explain pay-date timing.")],Limitation="Only a supplied fixture excerpt was compared; whole-page gaps and rankings remain unverified."});
            model.GenerateAsync<ArticleTermResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ArticleTermResponse{Mappings=[new("pay dates","Bill plan","Maps expected cash arrival to bill timing.")]});
            model.GenerateAsync<ArticleOutlineResult>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ArticleOutlineResult(idea.IdeaTitle,"LongFormBlogArticle",300,600,400,["Bill plan","Reserve example","Checklist","FAQ"],[new("plan","Bill plan"),new("reserve","Reserve example"),new("checklist","Checklist"),new("faq","FAQ")],true));
            model.GenerateAsync<ArticleVisualResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ArticleVisualResponse{Images=[new("Bill plan","Make the bill calendar readable",Table,"Illustrate this exact bill table without changing its cells","Example bill and due-date table")]});
            model.GenerateAsync<ArticleLinkResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ArticleLinkResponse{Links=[new("Bill plan","Record the date each bill falls due","https://example.org/bill-calendar")]});
            model.GenerateAsync<EditorialAssessmentResponse>(Arg.Any<string>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new EditorialAssessmentResponse{RubricVersion=EditorialRubric.ArticleProposal.Version,Dimensions=EditorialRubric.ArticleProposal.Dimensions.Select(d=>new EditorialDimensionAssessment{Key=d.Key,Rating=scenario=="invalidFinalAssessment"?5:4,Reason="Fixture judgment explains the actual useful bill plan and its limits.",EvidenceReferences=[Url]}).ToList()});
            var writer=Substitute.For<IWorkflowArticleWriter>();var draft=Draft();if(scenario=="changedDraftTopic")draft.Title="Unrelated tax help";
            writer.GenerateDraftAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<IReadOnlyCollection<string>>(),Arg.Any<IReadOnlyCollection<string>>(),Arg.Any<int>(),Arg.Any<int>(),Arg.Any<TopicExpansionResult?>(),Arg.Any<ArticleOutlineResult?>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(draft);
            var revisionCalls=0;
            writer.ReviseArticleAsync(Arg.Any<EditorialRevisionRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
            {var request=call.Arg<EditorialRevisionRequest>();var changed=JsonSerializer.Deserialize<WorkflowArticleDraft>(JsonSerializer.Serialize(request.Article))!;changed.IntroParagraphs[0]=changed.IntroParagraphs[0].Replace(revisionCalls++==0?"Plan bill timing":"Map bill timing",revisionCalls==1?"Map bill timing":"Organize bill timing",StringComparison.Ordinal);return Task.FromResult<WorkflowArticleDraft?>(changed);});
            var reviewer=Substitute.For<IArticleEvidenceReviewer>();reviewer.ReviewAsync(Arg.Any<GeneratedLongformArticle>(),Arg.Any<IReadOnlyList<string>>(),Arg.Any<CancellationToken>()).Returns(call=>Task.FromResult(Review(call.Arg<GeneratedLongformArticle>())));
            var inventory=Substitute.For<IPublishedArticleInventory>();inventory.ReadAsync(site.Id,Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<PublishedArticleTarget>>(scenario=="withPublishedLink"?[new("https://example.org/bill-calendar","A bill calendar","List bill dates in a calendar",DateTime.UtcNow)]:[]));
            var search=Substitute.For<IResearchSearchClient>();search.SearchAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<int>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<SearchResult>>([new(){Url=Url,Title="Fixture collected article",Content="Keep essential bill money available."}]));
            var handlers=ArticlePlanningWorkflowStepHandler.Capabilities.Select(key=>(IFullWorkflowStepHandler)new ArticlePlanningWorkflowStepHandler(key,db,model,writer,search,reviewer,scenario=="missingInventory"?null:inventory))
                .Concat(ArticleDeliveryWorkflowStepHandler.Capabilities.Select(key=>(IFullWorkflowStepHandler)new ArticleDeliveryWorkflowStepHandler(key,writer,reviewer,new ArticleTableSvgProvider()))).ToArray();
            var store=new FullWorkflowSpecificationStore(db);var specification=await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedArticleWorkflow.json"));
            var definition=await store.ImportDraftAsync(specification,family.Id,"OfflineFixture",default);
            Assert.ThrowsAsync<InvalidOperationException>(()=>store.ActivateAsync(definition.Id,handlers.Select(h=>h.CapabilityKey).ToArray(),"OfflineFixture",default),"Fixtures do not authorize production activation.");
            var runtime=new FullWorkflowStepRuntime(db,store,handlers);var run=await runtime.StartFixtureAsync(definition.Id,JsonSerializer.Serialize(new{ideaId=idea.Id,approvedAssetRoot=assetRoot,targetWordCountMin=300,targetWordCountMax=600}),default);
            await runtime.ProcessAsync(run.Id,default);var actual=await db.WorkflowDefinitionRuns.AsNoTracking().SingleAsync(r=>r.Id==run.Id);
            if(scenario is not ("success" or "withPublishedLink"))
            {
                Assert.That(actual.Status,Is.EqualTo(WorkflowDefinitionRunStatus.Failed),actual.ErrorMessage);
                var failed=await db.WorkflowStepRuns.AsNoTracking().SingleAsync(s=>s.Status==WorkflowDefinitionRunStatus.Failed);
                var expected=scenario switch{"inventedGapQuote"=>"AnalyzeCompetitorGaps","changedDraftTopic"=>"DraftArticle","missingInventory"=>"ApplyInternalLinks",_=>"RunFinalQaCompliance"};
                var capability=await db.WorkflowStepDefinitions.Where(s=>s.Id==failed.WorkflowStepDefinitionId).Select(s=>s.CapabilityKey).SingleAsync();
                Assert.That(capability,Is.EqualTo(expected),actual.ErrorMessage);
                if(scenario=="invalidFinalAssessment")
                {Assert.That(failed.OutputSnapshotJson,Does.Contain("InvalidAssessment"));Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="WorkflowStepFailedEvidence"),Is.EqualTo(1));}
                Assert.That(actual.CompletedUtc,Is.Null);return;
            }
            Assert.That(actual.Status,Is.EqualTo(WorkflowDefinitionRunStatus.AwaitingHumanApproval),actual.ErrorMessage);
            Assert.That(await db.WorkflowStepRuns.CountAsync(s=>s.Status==WorkflowDefinitionRunStatus.Completed),Is.EqualTo(22));
            Assert.That(await db.ContentArtifacts.CountAsync(a=>a.ArtifactType=="WorkflowStepEvidence"),Is.EqualTo(22));
            var final=await db.WorkflowStepRuns.OrderByDescending(s=>s.CompletedUtc).FirstAsync();using var output=JsonDocument.Parse(final.OutputSnapshotJson!);
            var state=output.RootElement.GetProperty("payload").Deserialize<ArticleDeliveryWorkflowState>()!;
            Assert.That(state.Package!.Html,Does.Contain("data:image/svg+xml;base64,").And.Contain("<table>"));
            Assert.That(state.Package.Markdown,Does.Contain("data:image/svg+xml;base64,"));Assert.That(state.EditorialAssessment!.Score,Is.EqualTo(100));
            Assert.That(state.PostEditReview!.ArticleVersionHash,Is.EqualTo(state.Package.ArticleVersionHash));Assert.That(state.ScoredPackageHash,Is.EqualTo(state.Package.PackageHash));
            Assert.That(state.AwaitingHumanApproval,Is.True);Assert.That(actual.CompletedUtc,Is.Null);Assert.That(revisionCalls,Is.EqualTo(2));
            Assert.That(reviewer.ReceivedCalls().Count(),Is.EqualTo(3));Assert.That(state.Scorecard!.SearchVolume,Is.Null);
            Assert.That(state.RefreshReviewUtc,Is.Not.Null);Assert.That(state.ContentVersionHistory.Distinct().Count(),Is.GreaterThanOrEqualTo(4));
            if(scenario=="withPublishedLink")Assert.That(state.Package.Markdown,Does.Contain("[Record the date each bill falls due](https://example.org/bill-calendar)"));
        }
        finally{if(Directory.Exists(assetRoot))Directory.Delete(assetRoot,true);}
    }
}
