using System.Reflection;
using System.Text.Json;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Research.SearXng;
using ContentOS.Infrastructure.Storage;
using ContentOS.Infrastructure.Image;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Infrastructure.Research;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class ArticleQualityRegressionTests
{
    [Test]
    public async Task UnsupportedCapabilityCannotProduceSimulatedSuccess()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = "Irregular income budget", ReaderProblem = "Income varies", AudienceType = "Build a baseline" };
        db.IdeaRecords.Add(idea);
        await db.SaveChangesAsync();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync(
            new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = idea.Id }, new WorkflowActionRun(),
            new WorkflowActionDefinition { Name = "Create Article Images", CapabilityKey = "NotImplementedImageCapability" }, default));
        Assert.That(error!.Message, Does.Contain("Unsupported article capability"));
        Assert.That(await db.ContentArtifacts.CountAsync(), Is.Zero);
    }
    [Test]
    public void SyntheticTemplateMustRetainItsProvenance()
    {
        var method = typeof(WorkflowCoordinatorAgent).GetMethod("BuildLongformArticle", BindingFlags.NonPublic | BindingFlags.Static)!;
        var article = (GeneratedLongformArticle)method.Invoke(null, new object?[] { new ContentIdea { Title = "Irregular income budget", PrimaryKeyword = "irregular income budget" }, new List<string>(), new List<string>() })!;
        Assert.That(article.IsSynthetic, Is.True);
        Assert.That(article.MeetsMinimumQuality, Is.False);
    }

    [Test]
    public async Task BlockedStrictQaCannotCompleteDispatch()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = "Irregular income budget", ReaderProblem = "Income varies", AudienceType = "Build a baseline" };
        db.IdeaRecords.Add(idea);
        await db.SaveChangesAsync();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        var run = new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = idea.Id };
        var action = new WorkflowActionDefinition { Name = "Pre-QA SEO Validation", CapabilityKey = "PreQaSeoValidation" };
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync(run, new WorkflowActionRun(), action, default));
        Assert.That(error!.Message, Does.StartWith("Article QA blocked:"));
    }

    [Test]
    public async Task FailedWriterDoesNotPersistSyntheticDraft()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = "Irregular income budget", ReaderProblem = "Income varies", AudienceType = "Build a baseline" };
        db.IdeaRecords.Add(idea);
        await db.SaveChangesAsync();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync(
            new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = idea.Id }, new WorkflowActionRun(),
            new WorkflowActionDefinition { Name = "Draft", CapabilityKey = "DraftArticle" }, default));
        Assert.That(error!.Message, Does.Contain("synthetic"));
        Assert.That(await db.ContentArtifacts.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task ValidStrictQaDispatchReturnsPass()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var article = ValidArticle();
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = article.Title, ReaderProblem = "Income varies", AudienceType = "Build a baseline",
            IdeaSnapshotJson = JsonSerializer.Serialize(new { primaryKeyword = "irregular income budget", secondaryKeywordsJson = "[\"variable earnings\",\"baseline spending\",\"cash reserve\"]" }) };
        var run = new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = idea.Id };
        db.IdeaRecords.Add(idea);
        db.ContentArtifacts.Add(new ContentArtifact { Id = Guid.NewGuid(), ContentWorkflowJobId = run.Id, ArtifactType = "DraftArticle", ContentJson = JsonSerializer.Serialize(article) });
        await db.SaveChangesAsync();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        var result = await dispatcher.DispatchAsync(run, new WorkflowActionRun(), new WorkflowActionDefinition { Name = "Pre-QA SEO Validation", CapabilityKey = "PreQaSeoValidation" }, default);
        Assert.That(((QaReportResult)result.Payload).QaStatus, Is.EqualTo("Pass"));
    }

    [Test]
    public async Task LegacySnapshotHydratesResearchWithoutChangingApprovedDirection()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var site = new Site { Id = Guid.NewGuid(), Name = "Eric's site", Domain = "example.test" };
        var research = new ContentIdea { Id = Guid.NewGuid(), SiteId = site.Id, Title = "Earlier title", PrimaryKeyword = "irregular income budget", SourceSummaryJson = "[\"Source: baseline example\"]", Summary = "Build a baseline", ContentType = "HowToArticle" };
        var idea = new IdeaRecord { Id = Guid.NewGuid(), IdeaTitle = "Approved title", ReaderProblem = "Income varies", AudienceType = "Build a baseline", IdeaSnapshotJson = JsonSerializer.Serialize(new { legacyContentIdeaId = research.Id }) };
        db.Sites.Add(site);
        db.ContentIdeas.Add(research);
        db.IdeaRecords.Add(idea);
        await db.SaveChangesAsync();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync(new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = idea.Id }, new WorkflowActionRun(), new WorkflowActionDefinition { Name = "Draft", CapabilityKey = "DraftArticle" }, default));
        var writer = provider.GetRequiredService<IWorkflowArticleWriter>();
        var arguments = writer.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "GenerateDraftAsync").GetArguments();
        Assert.That(arguments[1], Is.EqualTo("Approved title"));
        Assert.That(arguments[3], Is.EqualTo("irregular income budget"));
        Assert.That((IReadOnlyCollection<string>)arguments[11]!, Does.Contain("Source: baseline example"));
    }

    [Test]
    public async Task ResearchPersistenceFreezesWriterFieldsInSnapshot()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentOsDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var site = new Site { Id = Guid.NewGuid(), Name = "Eric's site", Domain = "example.test", IsActive = true };
        db.Sites.Add(site);
        await WorkflowTemplateSeeder.SeedAsync(db);
        var candidate = new CandidateContentIdea { Title = "Plan for varying pay", PrimaryKeyword = "irregular income budget", Summary = "Build a baseline", ContentType = "HowToArticle", SecondaryKeywords = new() { "cash reserve" }, SupportingFindings = new() { new ResearchFinding { SourceType = "Search", SourceTitle = "Baseline example", SourceUrl = "https://example.org/baseline", SourceExcerpt = "A $2100 baseline covers $1850 of essential bills; $250 remains for a reserve." } } };
        var ideation = Substitute.For<IIdeationAgent>();
        ideation.GenerateIdeasAsync(Arg.Any<ResearchContext>(), Arg.Any<IList<ResearchFinding>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<CandidateContentIdea>>(new List<CandidateContentIdea> { candidate }));
        var agent = new ResearchAgent(db, Array.Empty<IResearchSourceProvider>(), ideation, new IdeaDeduplicatorService(), NullLogger<ResearchAgent>.Instance);
        await agent.RunAsync(site.Id);
        var saved = await db.IdeaRecords.SingleAsync();
        using var snapshot = JsonDocument.Parse(saved.IdeaSnapshotJson);
        Assert.That(snapshot.RootElement.GetProperty("seoMeasurementStatus").GetString(), Is.EqualTo("Unknown"));
        Assert.That(snapshot.RootElement.GetProperty("competitionEvidenceStatus").GetString(), Is.EqualTo("Unknown"));
        Assert.That(snapshot.RootElement.TryGetProperty("primaryKeyword", out var keyword), Is.True, "Research must freeze the writer keyword.");
        Assert.That(keyword.GetString(), Is.EqualTo(candidate.PrimaryKeyword));
                Assert.That(snapshot.RootElement.GetProperty("sourceSummaryJson").GetString(), Does.Contain("Baseline example"));
        Assert.That(snapshot.RootElement.GetProperty("sourceSummaryJson").GetString(), Does.Contain("$1850"));
        Assert.That(snapshot.RootElement.GetProperty("sourceSummaryJson").GetString(), Does.Contain("https://example.org/baseline"));
        var dispatcher = scope.ServiceProvider.GetRequiredService<NewWorkflowRuntimeDispatcher>();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync(
            new WorkflowDefinitionRun { Id = Guid.NewGuid(), IdeaRecordId = saved.Id },
            new WorkflowActionRun(), new WorkflowActionDefinition { Name = "Draft", CapabilityKey = "DraftArticle" }, default));
        var writer = provider.GetRequiredService<IWorkflowArticleWriter>();
        var arguments = writer.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "GenerateDraftAsync").GetArguments();
        Assert.That(string.Join(" ", (IReadOnlyCollection<string>)arguments[11]!), Does.Contain("$1850"));
        Assert.That(string.Join(" ", (IReadOnlyCollection<string>)arguments[11]!), Does.Contain("https://example.org/baseline"));
    }

    [TestCase("A useful longform article also benefits from grounded evidence.", "meta-commentary")]
    [TestCase("Menu List Icon Table of contents Trend Unchanged Icon Arrow Up Icon", "navigation")]
    public async Task KnownHistoricalFillerIsBlocked(string badParagraph, string reason)
    {
        var article = ValidArticle();
        article.Sections[1].Paragraphs.Add(badParagraph);
        var result = await Review(article);
        Assert.That(result.HardRuleFailures.Any(f => f.Contains(reason, StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task ParagraphsRepeatedAcrossSectionsAreBlocked()
    {
        var article = ValidArticle();
        var paragraph = "Keep your baseline budget small enough to cover every essential bill during a quiet month.";
        article.Sections[1].Paragraphs.Add(paragraph);
        article.Sections[2].Paragraphs.Add(paragraph);
        var result = await Review(article);
        Assert.That(result.HardRuleFailures.Any(f => f.Contains("repeated paragraph", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task FaqHeadingWithoutQuestionsIsBlocked()
    {
        var article = ValidArticle();
        article.Sections.Single(s => s.Heading.Contains("FAQ")).Paragraphs.Clear();
        article.Sections.Single(s => s.Heading.Contains("FAQ")).Paragraphs.Add("Keep it simple and review what works.");
        var result = await Review(article);
        Assert.That(result.HardRuleFailures.Any(f => f.Contains("FAQ", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task SeparateFaqQuestionAndAnswerParagraphsPass()
    {
        var article = ValidArticle();
        var faq = article.Sections.Single(s => s.Heading.Contains("FAQ"));
        faq.Paragraphs.Clear();
        faq.Paragraphs.Add("What if income drops?");
        faq.Paragraphs.Add("Use your reserve to cover essentials and reduce optional spending.");
        Assert.That((await Review(article)).IsQualitySufficient, Is.True);
    }

    [Test]
    public async Task PracticalArticlePassesControl()
    {
        Assert.That((await Review(ValidArticle())).IsQualitySufficient, Is.True);
    }

    [Test]
    public void ApprovedSnapshotPreservesKeywordAndEvidence()
    {
        var snapshot = new IdeaRecord
        {
            IdeaTitle = "Budget when pay varies", ReaderProblem = "Income varies", AudienceType = "Build a baseline",
            IdeaSnapshotJson = JsonSerializer.Serialize(new { primaryKeyword = "irregular income budget", secondaryKeywordsJson = "[\"cash reserve\"]", sourceSummaryJson = "[\"Source: baseline example\"]", summary = "Budget with a baseline", contentType = "HowToArticle", slugSuggestion = "pay-varies" })
        };
        var method = typeof(NewWorkflowRuntimeDispatcher).GetMethod("BuildCompatIdea", BindingFlags.NonPublic | BindingFlags.Static)!;
        var idea = (ContentIdea)method.Invoke(null, new object[] { snapshot })!;
        Assert.That(idea.PrimaryKeyword, Is.EqualTo("irregular income budget"));
        Assert.That(idea.SourceSummaryJson, Does.Contain("baseline example"));
        Assert.That(idea.SecondaryKeywordsJson, Does.Contain("cash reserve"));
        Assert.That(idea.ContentType, Is.EqualTo("HowToArticle"));
    }

    [Test]
    public void SourceExtractionPrefersArticleOverUiDebris()
    {
        var method = typeof(SearXngContentExtractor).GetMethod("ExtractTextFromHtml", BindingFlags.NonPublic | BindingFlags.Static)!;
        var html = "<div>Menu List Icon Table of contents Trend Unchanged Icon Arrow Up Icon</div>" +
            "<article><p>A $2100 baseline covers $1850 of essential bills and leaves $250 for a reserve.</p></article>";
        var text = (string)method.Invoke(null, new object[] { html })!;
        Assert.That(text, Does.Contain("$1850"));
        Assert.That(text, Does.Not.Contain("Icon"));
    }

    [Test]
    public void UnclosedScriptDoesNotCrashSourceExtraction()
    {
        var method = typeof(SearXngContentExtractor).GetMethod("ExtractTextFromHtml", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.DoesNotThrow(() => method.Invoke(null, new object[] { "<p>Keep enough money aside for essential bills.</p><script>unfinished" }));
    }

    [Test]
    public async Task QuestionOnlyFaqIsBlocked()
    {
        var article = ValidArticle();
        var faq = article.Sections.Single(s => s.Heading.Contains("FAQ"));
        faq.Paragraphs.Clear();
        faq.Paragraphs.Add("What if income drops?");
        faq.Paragraphs.Add("How much should I save?");
        Assert.That((await Review(article)).IsQualitySufficient, Is.False);
    }

    [Test]
    public void SourceExtractionPrefersMainOverRelatedStoryTeaser()
    {
        var method = typeof(SearXngContentExtractor).GetMethod("ExtractTextFromHtml", BindingFlags.NonPublic | BindingFlags.Static)!;
        var html = "<article><p>Related story teaser.</p></article><main><p>A $2100 baseline covers $1850 of essential bills and leaves $250 for a reserve.</p></main>";
        var text = (string)method.Invoke(null, new object[] { html })!;
        Assert.That(text, Does.Contain("$1850"));
        Assert.That(text, Does.Not.Contain("teaser"));
    }
    [TestCase("Frequently Asked Questions")]
    [TestCase("Frequently   Asked Questions")]
    public async Task ExpandedFaqHeadingWithAnsweredQuestionPasses(string heading)
    {
        var article = ValidArticle();
        article.Sections[4] = new(heading, article.Sections[4].Paragraphs);
        Assert.That((await Review(article)).IsQualitySufficient, Is.True);
    }

    [TestCase("Keep it simple and review what works.")]
    [TestCase("What if income drops?\nHow much should I save?")]
    public async Task ExpandedFaqHeadingStillRequiresAnAnswer(string text)
    {
        var article = ValidArticle();
        article.Sections[4] = new("Frequently Asked Questions", new() { text });
        Assert.That((await Review(article)).HardRuleFailures.Any(f => f.Contains("FAQ")), Is.True);
    }

    [TestCase("What You\u2019ll Learn")]
    [TestCase("What You\u2018ll Learn")]
    [TestCase("What   You\u2019ll   Learn")]
    public async Task TypographicLearnHeadingPasses(string heading)
    {
        var article = ValidArticle();
        article.Sections[0] = new(heading, article.Sections[0].Paragraphs);
        Assert.That((await Review(article)).IsQualitySufficient, Is.True);
    }

    [Test]
    public async Task NamedWorkedExampleWithActualCalculationPassesWithoutMagicPhrase()
    {
        var article = ValidArticle();
        article.Sections[1] = new("Clearly Labeled Hypothetical Worked Example", new()
        {
            "Jordan starts with $300 and receives $700. Housing costs $750, groceries $120, and transport $80.",
            "Week one calculation: $300 + $700 \u2212 $750 \u2212 $120 \u2212 $80 = $50. Carry $50 into week two."
        });
        Assert.That((await Review(article)).IsQualitySufficient, Is.True);
    }

    [TestCase("Use a cautious budget and review it weekly.")]
    [TestCase("Opening balance + income - bills = ending balance.")]
    [TestCase("See https://example.test/worked-example/300+700-750=250 for the method.")]
    public async Task NamedExampleWithoutConcreteCalculationIsStillBlocked(string text)
    {
        var article = ValidArticle();
        article.Sections[1] = new("Worked Example", new() { text });
        Assert.That((await Review(article)).HardRuleFailures.Any(f => f.Contains("no worked example")), Is.True);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices($"Data Source=quality-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        services.RemoveAll<IWorkflowArticleWriter>();
        services.AddSingleton(Substitute.For<IWorkflowArticleWriter>());
        services.RemoveAll<IFileStorageService>();
        services.AddSingleton(Substitute.For<IFileStorageService>());
        services.RemoveAll<IImageGenerationService>();
        services.AddSingleton(Substitute.For<IImageGenerationService>());
        return services.BuildServiceProvider();
    }

    private static Task<QaReportResult> Review(GeneratedLongformArticle article)
    {
        // Rebuild text as a real draft consumer does; no provider or HTTP calls occur.
        article = article with { FullText = string.Join("\n\n", article.IntroParagraphs.Concat(article.Sections.SelectMany(s => s.Paragraphs))) };
        return new QaAndComplianceAgent().RunFinalQaReviewAsync(new ContentIdea { PrimaryKeyword = "irregular income budget" }, article, new[] { "variable earnings", "baseline spending", "cash reserve" }, Array.Empty<string>());
    }

    private static GeneratedLongformArticle ValidArticle()
    {
        var sections = new List<GeneratedSection>
        {
            new("What You'll Learn", new() { "- Set a baseline.\n- Cover essentials.\n- Keep a reserve." }),
            new("Your baseline", new() { "For example, a $2100 baseline covers $1850 in essentials and leaves $250 for a reserve." }),
            new("Step by step", new() { "1. Add your essential bills.\n2. Compare them with your lowest income month." }),
            new("Tools", new() { "Use a simple worksheet to list rent, food, utilities and transport separately." }),
            new("FAQ", new() { "What if income drops? Use your reserve to cover essentials and reduce optional spending." }),
            new("Income timing", new() { "Match bill dates to expected deposits so each bill has money set aside." }),
            new("Review", new() { "Check your plan weekly and adjust optional spending after essentials are covered." }),
            new("Reserve", new() { "Set aside surplus from stronger months to support a weaker month." })
        };
        return new("Irregular income budget", "irregular-income-budget", "Build a baseline", "Budget for variable earnings", 1800, 2600, 1900, 8,
            new() { "Income varies. Your essential bills keep arriving.", "You're not alone. Start with your lowest reliable month.", "A small reserve makes the next quiet month easier." }, sections, new() { "Review your baseline each month." }, "Fill in your baseline worksheet today.", "", "", false, true);
    }
}
