using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Writing;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class EditorialRevisionRegressionTests
{
    private static GeneratedLongformArticle Article() => new("Match pay to bills", "match-pay", "Plan cash", "Plan essentials", 100, 300, 40, 1,
        ["It is important to consider a $2100 baseline."], [new("Protect essentials", ["Keep $1850 for essentials. [Source](https://example.org/source)", "- List bills\n- Check dates", "| Bill | Amount |\n| --- | --- |\n| Rent | $1000 |"] )], ["Check the next pay date."], "List your next bills.", "original", "original", false, true);
    private static WorkflowArticleDraft Revised(GeneratedLongformArticle article) => new()
    { Title = article.Title, Slug = article.Slug, Summary = article.Summary, MetaDescription = article.MetaDescription,
      IntroParagraphs = ["Start with a $2100 baseline."], Sections = article.Sections.Select(s => new WorkflowArticleSectionDraft { Heading = s.Heading, Paragraphs = s.Paragraphs.ToList() }).ToList(), ConclusionParagraphs = article.ConclusionParagraphs.ToList(), CallToAction = article.CallToAction };

    [Test]
    public async Task ActualRevisionChangesProseAndRecomputesVersionWithoutMutatingInput()
    {
        var original = Article(); var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.ReviseArticleAsync(Arg.Any<EditorialRevisionRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<WorkflowArticleDraft?>(Revised(original)));
        var result = await new EditorialRevisionService(writer).ReviseAsync(original, "Direct, calm and useful", "Tighten prose", default);
        Assert.That(result.Article.IntroParagraphs.Single(), Is.EqualTo("Start with a $2100 baseline."));
        Assert.That(original.IntroParagraphs.Single(), Does.StartWith("It is important"));
        Assert.That(result.InputHash, Is.Not.EqualTo(result.OutputHash));
        Assert.That(result.RequiresPostEditFactSourceLinkReview, Is.True);
        Assert.That(result.PublishReady, Is.False);
        Assert.That(result.Article.BodyText, Does.Contain("Start with a $2100 baseline."));
        Assert.That(result.Article.EstimatedWordCount, Is.GreaterThan(0));
    }

    [TestCase("number")]
    [TestCase("source")]
    [TestCase("heading")]
    [TestCase("table")]
    [TestCase("bullet")]
    [TestCase("unchanged")]
    public void RevisionCannotDiscardVerifiedContentOrClaimAnUnchangedDraftWasEdited(string defect)
    {
        var original = Article(); var draft = Revised(original);
        switch(defect)
        {
            case "number": draft.IntroParagraphs[0] = "Start with a $2300 baseline."; break;
            case "source": draft.Sections[0].Paragraphs[0] = "Keep $1850 for essentials."; break;
            case "heading": draft.Sections[0].Heading = "Tax preparation help"; break;
            case "table": draft.Sections[0].Paragraphs[2] = "Rent costs $1000."; break;
            case "bullet": draft.Sections[0].Paragraphs[1] = "List bills and check dates."; break;
            case "unchanged": draft.IntroParagraphs = original.IntroParagraphs.ToList(); break;
        }
        var writer = Substitute.For<IWorkflowArticleWriter>();
        writer.ReviseArticleAsync(Arg.Any<EditorialRevisionRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<WorkflowArticleDraft?>(draft));
        Assert.ThrowsAsync<InvalidOperationException>(() => new EditorialRevisionService(writer).ReviseAsync(original, "Calm", "Edit", default));
    }

    [Test]
    public void MissingRevisionProviderBlocksInsteadOfReturningReportOnlySuccess()
    {
        var writer = Substitute.For<IWorkflowArticleWriter>();
        Assert.ThrowsAsync<InvalidOperationException>(() => new EditorialRevisionService(writer).ReviseAsync(Article(), "Calm", "Edit", default));
    }
}
