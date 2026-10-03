using System.Net;
using System.Reflection;
using System.Text.Json;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Configuration;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Research.SeoData;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public sealed class TitleKeywordRegressionTests
{
    private const string Main = "how to budget when freelance income changes every month";
    private const string Question = "How much should I keep for a month with no freelance income?";

    [Test]
    public async Task SearchResultsCannotBecomeMeasuredKeywordMetrics()
    {
        Assert.That(await Provider().GetKeywordMetricsAsync(Main), Is.Null);
    }

    [Test]
    public async Task SearchResultsCannotBecomeRandomMeasuredTrendHistory()
    {
        Assert.That(await Provider().GetTrendDataAsync(Main), Is.Null);
    }

    [Test]
    public async Task SearchTitlesAreNotMeasuredRelatedQueries()
    {
        Assert.That(await Provider().GetRelatedKeywordsAsync(Main), Is.Empty);
    }

    [Test]
    public async Task StrategyPreservesCompleteMainAndRelatedQuestionsWithoutInventingVariants()
    {
        var result = await Strategy(Substitute.For<ILlmClient>()).BuildKeywordStrategyAsync(Idea(), ["cash reserve for freelance lean months", Question]);
        Assert.Multiple(() =>
        {
            Assert.That(result.PrimaryKeyword, Is.EqualTo(Main));
            Assert.That(result.SecondaryKeywords, Is.EquivalentTo(new[] { "cash reserve for freelance lean months", Question }));
            Assert.That(result.FaqQuestions, Does.Contain(Question));
            Assert.That(result.TitleOptions, Is.EqualTo(new[] { Idea().Title }));
        });
    }

    [Test]
    public void StrategyRejectsMissingPrimaryInsteadOfUsingDisplayTitle()
    {
        var idea = Idea(); idea.PrimaryKeyword = "";
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Strategy(Substitute.For<ILlmClient>()).BuildKeywordStrategyAsync(idea, [Question]));
    }

    [Test]
    public void CompatSnapshotLeavesMissingPrimaryUnknownInsteadOfUsingDisplayTitle()
    {
        var method = typeof(NewWorkflowRuntimeDispatcher).GetMethod("BuildCompatIdea", BindingFlags.NonPublic | BindingFlags.Static)!;
        var idea = (ContentIdea)method.Invoke(null, [new IdeaRecord { IdeaTitle = "A display headline is not a keyword", IdeaSnapshotJson = "{}" }])!;
        Assert.That(idea.PrimaryKeyword, Is.Empty);
    }

    [Test]
    public void CandidateSnapshotPreservesKeywordsQuestionsAndCollectedEvidence()
    {
        var candidate = new CandidateContentIdea { Title = "SENSITIVE_TEST_CONTENT_do_not_log", PrimaryKeyword = Main, AudiencePainPoint = "Variable pay complicates bill planning", AudienceGoal = "Cover essential bills", SearchIntent = "informational", RecommendedAngle = "practical", SecondaryKeywords = [Question], SupportingFindings = [new ResearchFinding { SourceUrl = "https://example.org/budget", SourceExcerpt = "Set aside $1850 for essential bills." }] };
        var method = typeof(NewWorkflowRuntimeDispatcher).GetMethod("BuildCompatIdea", BindingFlags.NonPublic | BindingFlags.Static)!;
        var restored = (ContentIdea)method.Invoke(null, [new IdeaRecord { IdeaTitle = candidate.Title, IdeaSnapshotJson = JsonSerializer.Serialize(candidate) }])!;
        Assert.Multiple(() =>
        {
            Assert.That(restored.PrimaryKeyword, Is.EqualTo(Main));
            Assert.That(JsonSerializer.Deserialize<string[]>(restored.SecondaryKeywordsJson), Is.EqualTo(new[] { Question }));
            Assert.That(restored.SourceSummaryJson, Does.Contain("$1850").And.Contain("https://example.org/budget"));
        });
    }

    [Test]
    public void WriterPreservesCompleteKeywordsAndQuestions()
    {
        var prompt = WriterPrompt(Main, ["cash reserve for freelance lean months", Question]);
        Assert.That(prompt, Does.Contain("Primary Keyword: " + Main));
        Assert.That(prompt, Does.Contain("Secondary Keywords: cash reserve for freelance lean months, " + Question));
        Assert.That(prompt, Does.Contain("Must-Answer Questions:").And.Contain(Question));
    }

    [Test]
    public void WriterRejectsMissingPrimaryInsteadOfUsingDisplayTitle()
    {
        var error = Assert.Throws<TargetInvocationException>(() => WriterPrompt("", [Question]));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task TitleRefinementAcceptsPlainTextThroughRealClientAndUsesArticleEvidence()
    {
        var handler = new ResponseHandler("Budget for lean freelance months before spending extra pay");
        var client = new OllamaLlmClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") }, NullLogger<OllamaLlmClient>.Instance);
        var title = await Strategy(client).RefineAndSelectBestTitleAsync(Idea(), Article(), [Question]);
        Assert.That(title, Is.EqualTo(handler.Text));
        using var payload = JsonDocument.Parse(handler.Request!);
        var prompt = payload.RootElement.GetProperty("prompt").GetString()!;
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("$1850").And.Contain(Question));
            Assert.That(prompt, Does.Contain("untrusted").And.Contain("Do not invent"));
            Assert.That(prompt, Does.Not.Contain("Emotional Trigger (0-5)").And.Not.Contain("Clickability"));
            Assert.That(prompt, Does.Not.Contain("5 distinct title variations"));
        });
    }

    [Test]
    public async Task TitleRefinementRejectsInventedNumbersInsteadOfRewardingThem()
    {
        var llm = Substitute.For<ILlmClient>();
        const string unsupported = "Build a $9999 reserve for lean freelance months";
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(unsupported);
        llm.GenerateAsync<string>(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(unsupported);
        Assert.That(await Strategy(llm).RefineAndSelectBestTitleAsync(Idea(), Article(), []), Is.EqualTo(Idea().Title));
    }

    [Test]
    public async Task ReaderGoalNumberCannotSupportNumericArticlePromise()
    {
        var idea = Idea(); idea.AudienceGoal = "Save $9999";
        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("Build a $9999 reserve for lean freelance months");
        Assert.That(await Strategy(llm).RefineAndSelectBestTitleAsync(idea, Article(), []), Is.EqualTo(idea.Title));
    }

    [Test]
    public async Task ActualBriefPreservesPrimaryRelatedPhrasesAndParentheticalIntent()
    {
        var idea = Idea(); idea.PrimaryKeyword = "budgeting for freelancers (UK)";
        const string related = "How do UK freelancers budget for tax - before April?";
        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(idea.Title);
        var brief = await Strategy(llm).BuildContentBriefAsync(idea, Article(), [related], []);
        Assert.That(brief.KeywordCluster, Is.EqualTo(new[] { idea.PrimaryKeyword, related }));
    }

    [Test]
    public void RefinementPropagatesCancellationInsteadOfReturningApprovedFallback()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await Strategy(Substitute.For<ILlmClient>()).RefineAndSelectBestTitleAsync(Idea(), Article(), [], cancellation.Token));
    }

    [Test]
    public async Task EditorialPackRetainsActualHeadlineAndIntroInsteadOfGenericClaims()
    {
        var article = Article();
        var result = await new EditorialAgent().ImproveHeadlineAndHookAsync(article, Main);
        Assert.Multiple(() =>
        {
            Assert.That(result.RecommendedHeadline, Is.EqualTo(article.Title));
            Assert.That(result.AlternateHeadlines, Is.Empty);
            Assert.That(result.HookLines, Is.EqualTo(article.IntroParagraphs));
            Assert.That(string.Join(" ", result.HookLines), Does.Not.Contain("first 30 days"));
        });
    }

    [Test]
    public void EmptyIdeationCannotSilentlyProduceCompleteGuideFallback()
    {
        var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdeationResponse?>(null));
        var agent = new IdeationAgent(writer, Substitute.For<ITopicDiversityScorer>(), new IdeaDeduplicatorService(), NullLogger<IdeationAgent>.Instance);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.GenerateIdeasAsync(new ResearchContext { Niche = "freelance budgeting" }, [new ResearchFinding { KeywordSuggestion = Main, SourceTitle = "An example" }]));
    }

    [Test]
    public async Task IdeationDoesNotRequireUnsupportedNumbersOrDeclareRankingPotentialMeasured()
    {
        var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdeationResponse?>(new() { Ideas = [new() { Title = Idea().Title, PrimaryKeyword = Main }] }));
        var agent = new IdeationAgent(writer, Substitute.For<ITopicDiversityScorer>(), new IdeaDeduplicatorService(), NullLogger<IdeationAgent>.Instance);
        await agent.GenerateIdeasAsync(new ResearchContext(), [new ResearchFinding { KeywordSuggestion = Main, SourceTitle = "An example", SourceUrl = "https://example.org/budget", SourceExcerpt = "Pay varies; plan essential bills before spending extra income." }]);
        var prompt = (string)writer.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.That(prompt, Does.Not.Contain("Specificity is MANDATORY: real dollar amounts"));
        Assert.That(prompt, Does.Contain("volume").And.Contain("unknown").And.Contain("secondary"));
    }

    [Test]
    public async Task RealIdeationPreservesNaturalLongTailKeywordAndRelatedQuestion()
    {
        var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.GenerateIdeationResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IdeationResponse?>(new() { Ideas = [new() { Title = "SENSITIVE_TEST_CONTENT_do_not_log", PrimaryKeyword = Main, AudiencePainPoint = "Variable pay complicates bill planning", AudienceGoal = "Cover essential bills", SearchIntent = "informational", RecommendedAngle = "practical", SupportingSourceUrls = ["https://example.org/budget"], SecondaryKeywords = [Question] }] }));
        var captured = new WorkflowDiagnosticRegressionTests.CapturingLogger<IdeationAgent>();
        var agent = new IdeationAgent(writer, Substitute.For<ITopicDiversityScorer>(), new IdeaDeduplicatorService(), captured);
        var ideas = await agent.GenerateIdeasAsync(new ResearchContext(), [new ResearchFinding { KeywordSuggestion = Main, SourceTitle = "An example", SourceUrl = "https://example.org/budget", SourceExcerpt = "Pay varies; plan essential bills before spending extra income." }]);
        Assert.That(ideas, Has.Count.EqualTo(1));
        Assert.That(ideas[0].PrimaryKeyword, Is.EqualTo(Main));
        Assert.That(ideas[0].SecondaryKeywords, Is.EqualTo(new[] { Question }));
        Assert.That(ideas[0].SeoMeasurementStatus, Is.EqualTo("Unknown"));
        Assert.That(ideas[0].CompetitionEvidenceStatus, Is.EqualTo("Unknown"));
        Assert.That(ideas[0].SeoOpportunityScore, Is.Zero);
        Assert.That(ideas[0].LowCompetitionBoost, Is.Zero);
        Assert.That(captured.All, Does.Not.Contain("SENSITIVE_TEST_CONTENT_do_not_log"));
    }

    private static GoogleTrendsProvider Provider()
    {
        var search = Substitute.For<IResearchSearchClient>();
        search.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<SearchResult>>(Enumerable.Range(0, 10).Select(i => new SearchResult { Title = "A search headline " + i, Score = 5, Content = "A snippet" }).ToList()));
        return new(search, NullLogger<GoogleTrendsProvider>.Instance);
    }

    private static ContentStrategyAgent Strategy(ILlmClient client) => new(NullLogger<ContentStrategyAgent>.Instance, client, Options.Create(new SimulationSettings { UseSimulatedAgents = false }));
    private static ContentIdea Idea() => new() { Title = "Plan a budget around lean freelance months", PrimaryKeyword = Main, AudiencePainPoint = "Variable pay", AudienceGoal = "Cover essential bills", SearchIntent = "Informational", RecommendedAngle = "Use a baseline", WhyNow = "Evergreen", Summary = "Keep essentials covered" };
    private static GeneratedLongformArticle Article() => new(Idea().Title, "lean-month-budget", "Use a baseline", "Cover lean-month essentials", 1500, 2000, 1600, 8, ["Set aside $1850 for essential bills.", "Save extra pay for a lean month."], [new("Baseline", ["A $2100 baseline leaves $250 after essential bills."])], ["Review your bills."], "List essential bills.", "Set aside $1850 for bills.", "Set aside $1850 for bills.", false, true);

    private static string WriterPrompt(string primary, string[] secondary)
    {
        var method = typeof(OllamaWorkflowArticleWriter).GetMethod("BuildPrompt", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { "LongFormBlogArticle", Idea().Title, "budget", primary, "Baseline", "informational", "Variable pay", "Cover bills", "Practical", "Evergreen", secondary, Array.Empty<string>(), 1500, 2000, null, null, new TopicExpansionResult("Cover bills", [], [], [Question], [], true), null })!;
    }

    private sealed class ResponseHandler(string text) : HttpMessageHandler
    {
        public string Text { get; } = text;
        public string? Request { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { response = Text })) };
        }
    }
}
