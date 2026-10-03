using ContentOS.Infrastructure.Workflow;
using NUnit.Framework;
using System.Text.Json;
namespace ContentOS.Infrastructure.Tests;
public class SourceBoundArticleBriefContractTests
{
    private const string Quote="HTTPS protects a connection. It does not establish operator honesty.";
    private static string[] Sources()=>[JsonSerializer.Serialize(new{url="https://example.org/a",excerpt=Quote,inferredNotes="Notes cannot support a quote."}),JsonSerializer.Serialize(new{url="https://example.org/b",excerpt="Missing evidence must remain an explicit limitation."})];
    private static SourceBoundArticleBriefResponse Response(IReadOnlyList<ArticleBriefSource> sources)=>new(){WorkingTitle="Explain the reasons",Brief=["The reader cannot understand the label.","Use a relatable decision and a hypothetical example."],KeywordCluster=["explainable website check"],EstimatedWordCount=1800,IsQualitySufficient=true,EvidenceSelections=sources.Select(s=>new ArticleBriefEvidenceSelection(s.SourceId,s.CollectedExcerpt)).ToList()};
    [Test]public void OfflineReferenceBindingPreservesProseAndRejectsSummaryOrAmbiguousSources()
    {
        var original=Sources();var legacy=new ContentOS.Infrastructure.Agents.ContentBriefResult("Explain the reasons",["The reader cannot understand the label."],["explainable website check"],[Quote,"Missing evidence must remain an explicit limitation."],1800,true);
        var bound=SourceBoundArticleBriefContract.BindPreservedQuoteResponse(legacy,original);var validated=SourceBoundArticleBriefContract.Validate(bound,original,legacy.WorkingTitle,legacy.Brief[0],1600,2200);
        Assert.That(bound.Brief,Is.EqualTo(legacy.Brief));Assert.That(validated.EvidenceReferences.Select(r=>r.ExactQuote),Is.EqualTo(legacy.Evidence));
        Assert.Throws<InvalidOperationException>(()=>SourceBoundArticleBriefContract.BindPreservedQuoteResponse(legacy with{Evidence=["Encryption proves that the website is safe."]},original));
        var ambiguous=original.Append(JsonSerializer.Serialize(new{url="https://example.org/c",excerpt=Quote})).ToArray();
        Assert.Throws<InvalidOperationException>(()=>SourceBoundArticleBriefContract.BindPreservedQuoteResponse(legacy,ambiguous));
    }
    [Test]public void ExactQuotesAndStableIdsRetainOriginalSourceBytesAndHashes()
    {
        var original=Sources();var described=SourceBoundArticleBriefContract.Describe(original);var result=SourceBoundArticleBriefContract.Validate(Response(described),original,"Explain the reasons","The reader cannot understand the label.",1600,2200);
        Assert.That(result.Brief.Evidence,Is.EqualTo(original));Assert.That(result.EvidenceReferences.Select(r=>r.SourceHash),Is.EqualTo(described.Select(s=>s.SourceHash)));
        Assert.That(result.EvidenceReferences.Select(r=>r.ExactQuote),Is.EqualTo(described.Select(s=>s.CollectedExcerpt)));
        Assert.That(SourceBoundArticleBriefContract.Describe(original).Select(s=>s.SourceId),Is.EqualTo(described.Select(s=>s.SourceId)));
    }
    [TestCase("summary")][TestCase("unknownId")][TestCase("missingSource")][TestCase("notesQuote")][TestCase("changedTitle")][TestCase("missingProblem")][TestCase("missingQuote")][TestCase("changedEvidence")]
    public void UntraceableOrMissingEvidenceFailsClosed(string defect)
    {
        var original=Sources();var response=Response(SourceBoundArticleBriefContract.Describe(original));
        switch(defect)
        {
            case "summary":response.EvidenceSelections[0]=response.EvidenceSelections[0] with{ExactQuote="Encryption helps prove a website is safe."};break;
            case "unknownId":response.EvidenceSelections[0]=response.EvidenceSelections[0] with{SourceId="invented-source"};break;
            case "missingSource":response.EvidenceSelections.RemoveAt(1);break;
            case "notesQuote":response.EvidenceSelections[0]=response.EvidenceSelections[0] with{ExactQuote="Notes cannot support a quote."};break;
            case "changedTitle":response.WorkingTitle="An unrelated title";break;
            case "missingProblem":response.Brief=["A different reader problem."];break;
            case "missingQuote":response.EvidenceSelections[0]=response.EvidenceSelections[0] with{ExactQuote=""};break;
            case "changedEvidence":original[0]=original[0].Replace("HTTPS","HTTP",StringComparison.Ordinal);break;
        }
        Assert.Throws<InvalidOperationException>(()=>SourceBoundArticleBriefContract.Validate(response,original,"Explain the reasons","The reader cannot understand the label.",1600,2200));
    }
    [TestCase("plain summary")][TestCase("{}")][TestCase("{\"url\":\"http://127.0.0.1\",\"excerpt\":\"A supposedly collected claim.\"}")][TestCase("{\"url\":\"https://example.org\",\"excerpt\":\"\"}")]
    public void MalformedOrNonpublicCollectedDataBlocksBeforeModelCall(string source)=>Assert.Throws<InvalidOperationException>(()=>SourceBoundArticleBriefContract.Describe([source]));
}
