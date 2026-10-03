using System.Net;
using System.Reflection;
using System.Text.Json;
using ContentOS.Application.Research;
using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Image;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.SearXng;
using ContentOS.Infrastructure.Storage;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public sealed class WorkflowDiagnosticRegressionTests
{
    private const string Private = "SENSITIVE_TEST_CONTENT_do_not_log";

    [Test]
    public async Task FailedResearchSearchReportsStatusWithoutQueryOrResponseBody()
    {
        var http = new HttpClient(new FailedHandler());
        var logger = new CapturingLogger<SearXngResearchClient>();
        var extractor = new SearXngContentExtractor(http, new CapturingLogger<SearXngContentExtractor>());
        var client = new SearXngResearchClient(http, logger, extractor);
        Assert.ThrowsAsync<HttpRequestException>(async () => await client.SearchAsync(Private));
        await Task.CompletedTask;
        Assert.That(logger.All, Does.Not.Contain(Private));
        Assert.That(logger.All, Does.Contain("401").And.Contain("Check"));
    }

    [Test]
    public async Task FailedWriterLogsActionableStatusAndModelWithoutProviderBody()
    {
        var logger = new CapturingLogger<OllamaWorkflowArticleWriter>();
        var writer = new OllamaWorkflowArticleWriter(new HttpClient(new FailedHandler()) { BaseAddress = new Uri("https://example.invalid/?token=" + Private) }, logger, Substitute.For<IEditorialExemplarService>());
        await writer.GenerateDraftAsync("LongFormBlogArticle", Private, "budget", "irregular income budget", Private, "informational", "Variable pay", "Cover bills", "Practical", "Evergreen", [], [], 1500, 2000);
        Assert.Multiple(() =>
        {
            Assert.That(logger.All, Does.Not.Contain(Private));
            Assert.That(logger.All, Does.Contain("rejected").And.Contain("gemma4:31b-cloud").And.Contain("401"));
            Assert.That(logger.Keys, Does.Contain("ElapsedMilliseconds").And.Contain("Outcome").And.Contain("Provider"));
        });
    }

    [Test]
    public async Task InvalidStructuredModelOutputDoesNotLeakResponsePrefix()
    {
        var logger = new CapturingLogger<OllamaLlmClient>();
        var client = new OllamaLlmClient(new HttpClient(new InvalidModelHandler()) { BaseAddress = new Uri("https://example.invalid/") }, logger);
        await client.GenerateAsync<IdeationResponse>(Private);
        Assert.That(logger.All, Does.Not.Contain(Private));
        Assert.That(logger.All, Does.Contain("valid JSON").And.Contain("check"));
    }

    [Test]
    public async Task ResearchSummaryUsesObservedCountsAndIdsWithoutSourceOrIdeaText()
    {
        using var provider = BuildProvider(); using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var site = new Site { Id = Guid.NewGuid(), Name = Private, Domain = "example.invalid", Niche = "Freelance budgeting", IsActive = true };
        db.Sites.Add(site); await db.SaveChangesAsync();
        var finding = new ResearchFinding { SourceTitle = Private, SourceUrl = "https://example.invalid/?token=" + Private, SourceExcerpt = Private + " is test text.", KeywordSuggestion = "irregular income budget" };
        var source = Substitute.For<IResearchSourceProvider>();
        source.ResearchAsync(Arg.Any<ResearchContext>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<ResearchFinding>>([finding]));
        var ideas = Substitute.For<IIdeationAgent>();
        ideas.GenerateIdeasAsync(Arg.Any<ResearchContext>(), Arg.Any<IList<ResearchFinding>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IList<CandidateContentIdea>>([new() { Title = Private, PrimaryKeyword = "irregular income budget", SupportingFindings = [finding] }]));
        var logger = new CapturingLogger<ResearchAgent>();
        await new ResearchAgent(db, [source], ideas, new IdeaDeduplicatorService(), logger).RunAsync(site.Id);
        Assert.That(logger.All, Does.Not.Contain(Private));
        Assert.That(logger.All, Does.Contain("collected").And.Contain("not verified"));
        Assert.That(logger.Keys, Does.Contain("ResearchOperationId").And.Contain("UsableExcerptCount").And.Contain("SourceId"));
    }

    [Test]
    public void ArticleCheckpointLogsCountsAndIdsWithoutArticleText()
    {
        var logger = new CapturingLogger<WorkflowCoordinatorAgent>();
        using var provider = BuildProvider(logger);
        var coordinator = provider.GetRequiredService<IWorkflowCoordinatorAgent>();
        var article = new GeneratedLongformArticle(Private, "budget", Private, Private, 1500, 2000, 1600, 8, [Private], [new(Private, [Private])], [Private], Private, Private, Private, false, true);
        var method = typeof(WorkflowCoordinatorAgent).GetMethod("LogArticleCheckpoint", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var runId = Guid.NewGuid();
        method.Invoke(coordinator, [new ContentWorkflowTask { Id = Guid.NewGuid(), ContentWorkflowJobId = runId }, new ContentIdea { PrimaryKeyword = Private }, "before-qa", article, null, null]);
        Assert.That(logger.All, Does.Not.Contain(Private));
        Assert.That(logger.All, Does.Contain(runId.ToString()));
        Assert.That(logger.Keys, Does.Contain("EstimatedWordCount").And.Contain("SectionCount"));
    }

    [TestCase(1, "failed")]
    [TestCase(2, "retry")]
    [TestCase(2, "cancelled")]
    [TestCase(2, "cancelled-after-output")]
    public async Task RealEngineExplainsQaFailureAndRetryWithCorrelatedFields(int maxRetry, string outcome)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        await WorkflowTemplateSeeder.SeedAsync(db);
        var definition = await db.WorkflowDefinitions.FirstAsync(d => d.WorkflowType == WorkflowDefinitionType.Article);
        var action = await db.WorkflowActionDefinitions.FirstAsync(a => a.WorkflowDefinitionId == definition.Id && a.CapabilityKey == "PreQaSeoValidation");
        if (outcome == "cancelled-after-output") action.CapabilityKey = "HumanizeArticle"; // Exercise persisted output through a supported offline capability.
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = "Variable pay", ReaderProblem = "Cover essentials", AudienceType = "Freelancers", Status = IdeaRecordStatus.Approved, IdeaSnapshotJson = JsonSerializer.Serialize(new { primaryKeyword = "irregular income budget" }) };
        var run = new WorkflowDefinitionRun { Id = Guid.NewGuid(), WorkflowDefinitionFamilyId = definition.WorkflowDefinitionFamilyId, WorkflowDefinitionId = definition.Id, WorkflowType = WorkflowDefinitionType.Article, Version = definition.Version, IdeaRecordId = idea.Id, Status = WorkflowDefinitionRunStatus.Ready };
        var actionRun = new WorkflowActionRun { Id = Guid.NewGuid(), WorkflowDefinitionRunId = run.Id, WorkflowActionDefinitionId = action.Id, Name = action.Name, Order = action.Order, Status = WorkflowDefinitionRunStatus.Ready, MaxRetry = maxRetry };
        db.IdeaRecords.Add(idea); db.WorkflowDefinitionRuns.Add(run); db.WorkflowActionRuns.Add(actionRun); await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var logger = new CapturingLogger<NewWorkflowRuntimeEngine>();
        if (outcome == "cancelled") logger.OnMessage = message => { if (message.StartsWith("Agent dispatch started", StringComparison.Ordinal)) cancellation.Cancel(); };
        if (outcome == "cancelled-after-output") logger.OnMessage = message => { if (message.StartsWith("Output persisted", StringComparison.Ordinal)) cancellation.Cancel(); };
        var engine = new NewWorkflowRuntimeEngine(db, scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>(), logger);
        await engine.ProcessPendingRunsAsync(cancellation.Token);
        if (outcome == "cancelled-after-output")
        {
            Assert.That(actionRun.RetryCount, Is.Zero);
            db.ChangeTracker.Clear();
            var persistedAction = await db.WorkflowActionRuns.SingleAsync(a => a.Id == actionRun.Id);
            var persistedRun = await db.WorkflowDefinitionRuns.SingleAsync(r => r.Id == run.Id);
            Assert.That(persistedAction.Status, Is.EqualTo(WorkflowDefinitionRunStatus.InProgress));
            Assert.That(persistedRun.Status, Is.EqualTo(WorkflowDefinitionRunStatus.InProgress));
            Assert.That(logger.All, Does.Not.Contain("exhausted"));
            return;
        }
        if (outcome == "cancelled")
        {
            Assert.That(actionRun.RetryCount, Is.Zero);
            Assert.That(actionRun.Status, Is.EqualTo(WorkflowDefinitionRunStatus.Ready));
            Assert.That(logger.All, Does.Contain("cancelled").And.Not.Contain("exhausted"));
            return;
        }
        Assert.Multiple(() =>
        {
            Assert.That(logger.All, Does.Contain("quality checks").And.Contain("Review").And.Contain(run.Id.ToString()));
            Assert.That(logger.Keys, Does.Contain("ElapsedMilliseconds").And.Contain("Outcome").And.Contain("Attempt").And.Contain("FailureCode"));
            Assert.That(logger.States.SelectMany(x => x).Any(x => x.Key == "Outcome" && Equals(x.Value, outcome)), Is.True);
            Assert.That(actionRun.ErrorMessage, Does.Contain("quality checks").And.Contain("Review"));
        });
    }

    private static ServiceProvider BuildProvider(ILogger<WorkflowCoordinatorAgent>? logger = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddInfrastructureServices($"Data Source=diagnostics-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        services.RemoveAll<IWorkflowArticleWriter>(); services.AddSingleton(Substitute.For<IWorkflowArticleWriter>());
        services.RemoveAll<IFileStorageService>(); services.AddSingleton(Substitute.For<IFileStorageService>());
        services.RemoveAll<IImageGenerationService>(); services.AddSingleton(Substitute.For<IImageGenerationService>());
        if (logger is not null) services.AddSingleton(logger);
        return services.BuildServiceProvider();
    }

    private sealed class FailedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(Private + " Authorization: Bearer secret") });
    }
    private sealed class InvalidModelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { response = Private })) });
    }
    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public Action<string>? OnMessage { get; set; }
        public List<IReadOnlyList<KeyValuePair<string, object?>>> States { get; } = [];
        public string All => string.Join("\n", Messages.Concat(States.SelectMany(x => x).Select(x => x.Key + "=" + x.Value)));
        public string Keys => string.Join(" ", States.SelectMany(x => x).Select(x => x.Key));
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { if (state is IEnumerable<KeyValuePair<string, object?>> values) States.Add(values.ToList()); return null; }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception); OnMessage?.Invoke(message); Messages.Add(message); if (exception is not null) Messages.Add(exception.ToString());
            if (state is IEnumerable<KeyValuePair<string, object?>> values) States.Add(values.ToList());
        }
    }
}
