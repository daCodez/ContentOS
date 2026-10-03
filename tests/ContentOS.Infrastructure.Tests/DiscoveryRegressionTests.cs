using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Research.Providers;
using ContentOS.Infrastructure.Ideation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using ContentOS.Infrastructure.Research.SearXng;
using System.Net;
using ContentOS.Infrastructure.Writing;

namespace ContentOS.Infrastructure.Tests;

public sealed class DiscoveryRegressionTests
{
    [Test]
    public void IdeationDoesNotCallWriterWhenNoUsableEvidenceRemains()
    {
        var writer=Substitute.For<IWorkflowArticleWriter>();
        var agent=new IdeationAgent(writer,Substitute.For<ITopicDiversityScorer>(),new IdeaDeduplicatorService(),NullLogger<IdeationAgent>.Instance);
        Assert.ThrowsAsync<InvalidOperationException>(async()=>await agent.GenerateIdeasAsync(new ResearchContext(),new List<ResearchFinding>{new(){SourceUrl="file:///private",SourceExcerpt="unusable provenance"},new(){SourceUrl="https://example.org/empty"}}));
        Assert.That(writer.ReceivedCalls(),Is.Empty);
    }

    [Test]
    public async Task SuccessfulSearchStillDoesNotMeasureKeywordOpportunity()
    {
        var idea=new CandidateContentIdea{Title="Budget variable pay around bills",PrimaryKeyword="budget variable pay",LowCompetitionBoost=10,SeoOpportunityScore=99};
        var scorer=new TopicDiversityScorer(Search([new(){Url="https://example.org/guide",Content="Pay dates"}]),NullLogger<TopicDiversityScorer>.Instance);
        await scorer.ClassifyAndScoreAsync(new List<CandidateContentIdea>{idea},new TopicDiversityConfig());
        Assert.That(idea.CompetitionEvidenceStatus,Is.EqualTo("UnmeasuredSearchComparison"));
        Assert.That(idea.SeoMeasurementStatus,Is.EqualTo("Unknown"));
        Assert.That(idea.SeoOpportunityScore,Is.Zero);
        Assert.That(idea.LowCompetitionBoost,Is.Zero);
    }

    [Test]
    public async Task SearchComparisonLogsDoNotExposeKeywordText()
    {
        const string keyword="SENSITIVE_KEYWORD_do_not_log";
        var logger=new WorkflowDiagnosticRegressionTests.CapturingLogger<TopicDiversityScorer>();
        var scorer=new TopicDiversityScorer(Search([new(){Url="https://example.org/guide"}]),logger);
        await scorer.ClassifyAndScoreAsync(new List<CandidateContentIdea>{new(){Title="Budget apps for families",PrimaryKeyword=keyword}},new TopicDiversityConfig());
        Assert.That(logger.All,Does.Not.Contain(keyword));
        Assert.That(logger.All,Does.Contain("Unmeasured search comparison"));
    }

    [Test]
    public void BalancedEvidenceExcludesDuplicatesAndInvalidSources()
    {
        var findings=new List<ResearchFinding>{new(){ProviderName="Reddit",SourceUrl="https://example.org/same",SourceExcerpt="First question"},new(){ProviderName="Reddit",SourceUrl="file:///private",SourceExcerpt="Wrong scheme"},new(){ProviderName="Competitor",SourceUrl="https://example.org/same",SourceExcerpt="Duplicate"},new(){ProviderName="Competitor",SourceUrl="https://example.org/other",SourceExcerpt="Other evidence"},new(){ProviderName="Other",SourceUrl="https://example.org/empty"}};
        var selected=ResearchEvidenceHandoff.BalanceCollectedSources(findings);
        Assert.That(selected.Select(f=>f.SourceUrl),Is.EqualTo(new[]{"https://example.org/same","https://example.org/other"}));
    }

    [Test]
    public void CallerCancellationPropagatesWithoutScoringOpportunity()
    {
        var client=Search([]);using var cts=new CancellationTokenSource();cts.Cancel();
        client.SearchAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<int>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyCollection<SearchResult>>>(_=>throw new OperationCanceledException(cts.Token));
        var scorer=new TopicDiversityScorer(client,NullLogger<TopicDiversityScorer>.Instance);
        Assert.ThrowsAsync<OperationCanceledException>(async()=>await scorer.ClassifyAndScoreAsync(new List<CandidateContentIdea>{new(){PrimaryKeyword="budget"}},new TopicDiversityConfig(),cts.Token));
    }

    [TestCase(401)]
    [TestCase(200)]
    public void UnavailableSearchIsNotReturnedAsEmptySuccess(int status)
    {
        var http=new HttpClient(new SearchHandler((HttpStatusCode)status,"{}"));
        var client=new SearXngResearchClient(http,NullLogger<SearXngResearchClient>.Instance,new SearXngContentExtractor(http,NullLogger<SearXngContentExtractor>.Instance));
        Assert.ThrowsAsync<HttpRequestException>(async()=>await client.SearchAsync("budget questions"));
    }

    [Test]
    public async Task SearchEnforcesDomainBoundariesBeforeExtraction()
    {
        var content=new string('x',320);
        var json=System.Text.Json.JsonSerializer.Serialize(new { results=new[] {new {url="https://reddit.com/r/budget",title="real",content},new {url="https://notreddit.com/r/budget",title="fake",content},new {url="https://old.reddit.com/r/budget",title="subdomain",content},new {url="https://quora.com/x",title="other",content} } });
        var http=new HttpClient(new SearchHandler(HttpStatusCode.OK,json));
        var client=new SearXngResearchClient(http,NullLogger<SearXngResearchClient>.Instance,new SearXngContentExtractor(http,NullLogger<SearXngContentExtractor>.Instance));
        var results=await client.SearchAsync("budget",includeDomains:["reddit.com"],excludeDomains:["old.reddit.com"]);
        Assert.That(results.Select(r=>r.Url),Is.EqualTo(new[]{"https://reddit.com/r/budget"}));
    }

    [Test]
    public async Task ForumQueryKeepsConfiguredEngineSetAndExplicitDomainFilters()
    {
        var handler=new RecordingSearchHandler();var http=new HttpClient(handler);
        var client=new SearXngResearchClient(http,NullLogger<SearXngResearchClient>.Instance,new SearXngContentExtractor(http,NullLogger<SearXngContentExtractor>.Instance));
        await client.SearchAsync("site:reddit.com HTTPS padlock scam questions",includeDomains:["reddit.com"]);
        Assert.That(handler.Url,Does.Not.Contain("engines="));Assert.That(Uri.UnescapeDataString(handler.Url!),Does.Contain("site:reddit.com HTTPS padlock scam questions"));
    }
    private sealed class RecordingSearchHandler:HttpMessageHandler
    {
        public string? Url;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Url=request.RequestUri!.AbsoluteUri;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"results\":[]}")});}
    }
    private sealed class SearchHandler(HttpStatusCode status,string json):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(json)});
    }
    private static IResearchSearchClient Search(IReadOnlyCollection<SearchResult> results)
    {
        var client = Substitute.For<IResearchSearchClient>();
        client.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(results));
        return client;
    }

    [Test]
    public async Task RedditRetainsQuestionAndExcerptWithoutSeedTemplate()
    {
        var question = "How can I budget when my pay arrives on Fridays?";
        var results = new[] { new SearchResult { Title=question, Url="https://www.reddit.com/r/budget/comments/abc/question", Content="My pay arrives Fridays but rent is due Monday. How do I cover the gap?" }, new SearchResult { Title="Wrong host", Url="https://notreddit.com/x", Content="unrelated" } };
        var findings = await new RedditResearchProvider(Search(results)).ResearchAsync(new ResearchContext { SeedTopics=["irregular income"] }, default);
        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings.First().ObservedPhrase, Is.EqualTo(question));
        Assert.That(findings.First().SourceExcerpt, Does.Contain("rent is due Monday"));
        Assert.That(findings.First().TopicSuggestion, Is.EqualTo(question));
        Assert.That(findings.First().KeywordSuggestion, Is.Not.EqualTo("irregular income"));
    }

    [Test]
    public async Task WebCoverageDoesNotClaimMeasuredTrends()
    {
        var findings = await new TrendResearchProvider(Search([new() { Title="Budgeting seasonal pay", Url="https://example.org/budget", Content="Seasonal workers compare their pay dates." }])).ResearchAsync(new ResearchContext { SeedTopics=["budgeting"] }, default);
        Assert.That(findings, Is.Not.Empty);
        Assert.That(findings.All(f => f.SourceType != "Trend" && f.SourceExcerpt.Contains("Seasonal workers") && f.Notes.Contains("unknown") && !f.MonetizationHint.Contains("Trend-backed")), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EmptyOrFailedSearchDoesNotClaimOpportunity(bool fail)
    {
        var client=Search([]);
        if(fail) client.SearchAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<int>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<IReadOnlyCollection<string>?>(),Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyCollection<SearchResult>>>(_=>throw new HttpRequestException("offline"));
        var idea=new CandidateContentIdea { Title="How to budget with irregular income", PrimaryKeyword="how to budget with irregular income", AudiencePainPoint="Pay varies", RecommendedAngle="Lean months" };
        await new TopicDiversityScorer(client,NullLogger<TopicDiversityScorer>.Instance).ClassifyAndScoreAsync(new List<CandidateContentIdea>{idea},new TopicDiversityConfig());
        Assert.That(idea.SerpChecked,Is.False);
        Assert.That(idea.CompetitionEvidenceStatus,Is.EqualTo(fail?"UnavailableSearchFailure":"UnavailableNoResults"));
        Assert.That(idea.LowCompetitionBoost,Is.Zero);
        Assert.That(idea.SeoOpportunityScore,Is.Zero);
    }

    [Test]
    public void PromptContainsOtherProvidersEvenWhenFirstHasManyFindings()
    {
        var findings=Enumerable.Range(0,12).Select(i=>new ResearchFinding {ProviderName="Reddit",SourceUrl=$"https://reddit.com/{i}",SourceExcerpt=$"Question {i}"}).ToList();
        findings.Add(new ResearchFinding {ProviderName="Competitor",SourceUrl="https://example.org/unique",SourceExcerpt="UNIQUE_OTHER_PROVIDER"});
        var method=typeof(IdeationAgent).GetMethod("BuildIdeationPrompt",BindingFlags.NonPublic|BindingFlags.Static)!;
        var prompt=(string)method.Invoke(null,[new ResearchContext(),findings,new List<ResearchInsight>()])!;
        Assert.That(prompt,Does.Contain("UNIQUE_OTHER_PROVIDER"));
    }
}
