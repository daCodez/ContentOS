using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Research.Serp;
using ContentOS.Infrastructure.Scoring;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class OptimizationScoringServiceTests
{
    [Test]
    public void Compute_StrongStructuredArticle_ReturnsPublishReadyScore()
    {
        var service = new DefaultOptimizationScoringService();
        var idea = BuildIdea();
        var benchmark = BuildBenchmark();
        var article = BuildArticle(
            title: "Best budgeting apps for beginners: a simple plan that works",
            primaryKeyword: "best budgeting apps",
            repeatedPrimaryKeywordCount: 5,
            includeRelatedKeywords: true,
            includeLinks: true,
            addStuffing: false);

        var report = service.Compute(idea, article, benchmark);

        Assert.That(report.OptimizationScore, Is.GreaterThanOrEqualTo(75));
        Assert.That(report.HighPriorityScore, Is.GreaterThanOrEqualTo(70));
        Assert.That(report.StuffingRiskScore, Is.LessThan(60));
        Assert.That(report.PublishReady, Is.True);
    }

    [Test]
    public void Compute_StuffedArticle_AppliesPenaltyAndBlocksPublish()
    {
        var service = new DefaultOptimizationScoringService();
        var idea = BuildIdea();
        var benchmark = BuildBenchmark();
        var article = BuildArticle(
            title: "Best budgeting apps best budgeting apps best budgeting apps",
            primaryKeyword: "best budgeting apps",
            repeatedPrimaryKeywordCount: 18,
            includeRelatedKeywords: false,
            includeLinks: false,
            addStuffing: true);

        var report = service.Compute(idea, article, benchmark);

        Assert.That(report.StuffingRiskScore, Is.GreaterThanOrEqualTo(60));
        Assert.That(report.StuffingPenaltyApplied, Is.GreaterThan(0));
        Assert.That(report.PublishReady, Is.False);
    }

    [Test]
    public void Compute_ThinKeywordIdeas_AddsFallbackKeywordChips()
    {
        var service = new DefaultOptimizationScoringService();
        var idea = BuildIdea();
        idea.SecondaryKeywordsJson = "[]";
        var benchmark = new SerpBenchmarkReport
        {
            Query = "best budgeting apps",
            ResultCount = 10,
            RecommendedWordCountMin = 1800,
            RecommendedWordCountMax = 2600,
            RecommendedPrimaryKeywordMin = 4,
            RecommendedPrimaryKeywordMax = 8,
            RelatedKeywords = []
        };
        var article = BuildArticle(
            title: "Best budgeting apps for beginners: a simple plan that works",
            primaryKeyword: "best budgeting apps",
            repeatedPrimaryKeywordCount: 5,
            includeRelatedKeywords: false,
            includeLinks: true,
            addStuffing: false);

        var report = service.Compute(idea, article, benchmark);

        Assert.That(report.RelatedKeywordChips.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(report.RelatedKeywordChips.Any(x => string.Equals(x.Keyword, "best budgeting apps", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(report.RelatedKeywordChips.Any(x => x.Keyword.Contains("tips", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(report.RelatedKeywordChips.Any(x => x.Keyword.Contains("how to start", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    private static ContentIdea BuildIdea()
        => new()
        {
            Title = "Best budgeting apps for beginners",
            PrimaryKeyword = "best budgeting apps",
            SecondaryKeywordsJson = "[\"budgeting app\",\"money management app\",\"expense tracker\",\"savings app\"]",
            SearchIntent = "informational",
            RecommendedAngle = "simple beginner comparison with practical setup advice",
            WhyNow = "People need an easier way to manage bills and spending this year.",
            AudienceGoal = "start budgeting without feeling overwhelmed",
            Summary = "A practical guide to choosing and using budgeting apps.",
            SlugSuggestion = "best-budgeting-apps-for-beginners"
        };

    private static SerpBenchmarkReport BuildBenchmark()
        => new()
        {
            Query = "best budgeting apps",
            ResultCount = 10,
            RecommendedWordCountMin = 1800,
            RecommendedWordCountMax = 2600,
            RecommendedPrimaryKeywordMin = 4,
            RecommendedPrimaryKeywordMax = 8,
            RelatedKeywords =
            [
                new SerpKeywordBenchmark { Keyword = "budgeting app", RecommendedMin = 1, RecommendedMax = 3 },
                new SerpKeywordBenchmark { Keyword = "money management app", RecommendedMin = 1, RecommendedMax = 2 },
                new SerpKeywordBenchmark { Keyword = "expense tracker", RecommendedMin = 1, RecommendedMax = 2 },
                new SerpKeywordBenchmark { Keyword = "savings app", RecommendedMin = 1, RecommendedMax = 2 }
            ]
        };

    private static GeneratedLongformArticle BuildArticle(string title, string primaryKeyword, int repeatedPrimaryKeywordCount, bool includeRelatedKeywords, bool includeLinks, bool addStuffing)
    {
        var repeatedKeywordBlock = string.Join(" ", Enumerable.Repeat(primaryKeyword, repeatedPrimaryKeywordCount));
        var related = includeRelatedKeywords
            ? "A budgeting app can work like a money management app, expense tracker, and savings app when the setup stays simple."
            : string.Empty;
        var links = includeLinks
            ? "Sources: https://www.consumerfinance.gov/ and https://www.canada.ca/."
            : string.Empty;
        var stuffingSentence = addStuffing
            ? $"{primaryKeyword} helps because {primaryKeyword} is what people search when they want {primaryKeyword} fast."
            : "Use the app that feels easiest to keep using every week.";

        var sections = new List<GeneratedSection>
        {
            new("Problem section", new List<string> { "Most people quit because the setup feels heavier than the payoff.", stuffingSentence }),
            new("Data / Baseline", new List<string> { "A simple weekly review usually beats a complicated system that breaks by week two.", links }),
            new("Solution Framework", new List<string> { "Choose one app, connect only the accounts you need, and build the habit before adding complexity.", related }),
            new("Step-by-step plan", new List<string> { "- Pick one app.\n- Add your main spending categories.\n- Review once a week.", repeatedKeywordBlock }),
            new("Tips / Strategies", new List<string> { "Keep categories broad at first so you do not burn out." }),
            new("Tools that make this easier", new List<string> { "YNAB and EveryDollar both work if you pair them with a weekly routine." }),
            new("Common mistakes", new List<string> { "Do not rebuild the system every weekend." }),
            new("FAQ", new List<string> { "What if I miss a week? Restart the habit the next week without changing tools." })
        };

        var fullText = string.Join("\n\n", new[]
        {
            title,
            "Budgeting can feel embarrassing when money already feels tight.",
            "That is why a beginner-friendly system matters.",
            "This guide keeps the first step simple.",
            string.Join("\n\n", sections.Select(s => s.Heading + "\n" + string.Join("\n\n", s.Paragraphs))),
            "Conclusion",
            "Pick one app and try a 20-minute budget check this week.",
            "Download the checklist and start with one category today."
        });

        var estimatedWordCount = Math.Max(1900, fullText.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length);

        return new GeneratedLongformArticle(
            title,
            "best-budgeting-apps-for-beginners",
            "A practical guide to choosing and using budgeting apps.",
            "Best budgeting apps for beginners who want a simple way to start.",
            1800,
            2600,
            estimatedWordCount,
            10,
            [
                "Budgeting can feel embarrassing when money already feels tight.",
                "That is why a beginner-friendly system matters.",
                "This guide keeps the first step simple."
            ],
            sections,
            ["Pick one app and stick with it for one month before switching."],
            "Download the checklist and start with one category today.",
            string.Join("\n\n", sections.Select(s => $"## {s.Heading}\n\n{s.BodyText}")),
            fullText,
            false,
            true);
    }
}
