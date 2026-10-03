using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using System.Text;
using System.Text.Json;
namespace ContentOS.Infrastructure.Workflow;
public sealed record ArticleBriefSource(string SourceId,string SourceHash,string SourceUrl,string CollectedExcerpt);
public sealed record ArticleBriefEvidenceSelection(string SourceId,string ExactQuote);
public sealed record ArticleBriefEvidenceReference(string SourceId,string SourceHash,string SourceUrl,string ExactQuote);
public sealed class SourceBoundArticleBriefResponse
{
    public string WorkingTitle {get;set;}="";
    public List<string> Brief {get;set;}=[];
    public List<string> KeywordCluster {get;set;}=[];
    public List<ArticleBriefEvidenceSelection> EvidenceSelections {get;set;}=[];
    public int EstimatedWordCount {get;set;}
    public bool IsQualitySufficient {get;set;}
}
public sealed record ValidatedSourceBoundBrief(ContentBriefResult Brief,IReadOnlyList<ArticleBriefEvidenceReference> EvidenceReferences);
public static class SourceBoundArticleBriefContract
{
    public static IReadOnlyList<ArticleBriefSource> Describe(IReadOnlyList<string> originalSources)
    {
        if(originalSources.Count is <1 or >12)throw new InvalidOperationException("Brief requires explicit collected source records.");
        var described=new List<ArticleBriefSource>();
        foreach(var original in originalSources)
        {
            try
            {
                using var document=JsonDocument.Parse(original);var data=document.RootElement;
                if(data.ValueKind!=JsonValueKind.Object||!data.TryGetProperty("url",out var url)||url.ValueKind!=JsonValueKind.String||!data.TryGetProperty("excerpt",out var excerpt)||excerpt.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(excerpt.GetString()))throw new InvalidOperationException("Collected source lacks an actual URL/excerpt.");
                _=ArticlePublicPageReader.ValidateUrl(url.GetString()!);
                var hash=FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(original));
                described.Add(new("source-"+hash[..16].ToLowerInvariant(),hash,url.GetString()!,excerpt.GetString()!));
            }
            catch(JsonException ex){throw new InvalidOperationException("Malformed collected source cannot support the brief.",ex);}
        }
        if(described.Select(s=>s.SourceId).Distinct(StringComparer.Ordinal).Count()!=described.Count||described.Select(s=>s.SourceUrl).Distinct(StringComparer.Ordinal).Count()!=described.Count)throw new InvalidOperationException("Ambiguous duplicate collected brief sources.");
        return described;
    }
    public static ValidatedSourceBoundBrief Validate(SourceBoundArticleBriefResponse response,IReadOnlyList<string> originalSources,string title,string readerProblem,int minimum,int maximum)
    {
        var sources=Describe(originalSources);var byId=sources.ToDictionary(s=>s.SourceId,StringComparer.Ordinal);
        if(response.WorkingTitle!=title||response.Brief is null||response.Brief.Count==0||response.Brief.Any(string.IsNullOrWhiteSpace)||!response.Brief.Any(b=>b.Contains(readerProblem,StringComparison.Ordinal))||response.EstimatedWordCount<minimum||response.EstimatedWordCount>maximum||response.KeywordCluster is null||response.EvidenceSelections is null||response.EvidenceSelections.Count is <1 or >40)throw new InvalidOperationException("Brief changed the approved title/problem/length or lacks actual evidence selections.");
        var references=new List<ArticleBriefEvidenceReference>();
        foreach(var selection in response.EvidenceSelections)
        {
            if(selection is null||!byId.TryGetValue(selection.SourceId??"",out var source)||string.IsNullOrWhiteSpace(selection.ExactQuote)||selection.ExactQuote.Length<12||!source.CollectedExcerpt.Contains(selection.ExactQuote,StringComparison.Ordinal))throw new InvalidOperationException("Brief evidence contains an invented source ID, absent quote, paraphrase or text outside the collected excerpt.");
            references.Add(new(source.SourceId,source.SourceHash,source.SourceUrl,selection.ExactQuote));
        }
        if(!references.Select(r=>r.SourceId).ToHashSet(StringComparer.Ordinal).SetEquals(byId.Keys))throw new InvalidOperationException("Brief omits a required collected source.");
        // Source bytes remain controller-owned. The model selects IDs and quotes; it never reconstructs provenance.
        return new(new(response.WorkingTitle,response.Brief.ToArray(),response.KeywordCluster.ToArray(),originalSources.ToArray(),response.EstimatedWordCount,response.IsQualitySufficient),references);
    }
    /// <summary>Offline reference binding of a preserved response. No generation, ambiguity resolution or prose repair.</summary>
    public static SourceBoundArticleBriefResponse BindPreservedQuoteResponse(ContentBriefResult preserved,IReadOnlyList<string> originalSources)
    {
        var sources=Describe(originalSources);
        if(preserved.Evidence is null||preserved.Evidence.Length==0)throw new InvalidOperationException("Preserved response has no original quotes.");
        var selections=new List<ArticleBriefEvidenceSelection>();
        foreach(var quote in preserved.Evidence)
        {
            if(string.IsNullOrWhiteSpace(quote))throw new InvalidOperationException("Preserved quote is absent.");
            var matches=sources.Where(s=>s.CollectedExcerpt.Contains(quote,StringComparison.Ordinal)).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Preserved evidence is a paraphrase, unmatched or ambiguous; offline binding cannot invent a source choice.");
            selections.Add(new(matches[0].SourceId,quote));
        }
        return new(){WorkingTitle=preserved.WorkingTitle,Brief=preserved.Brief.ToList(),KeywordCluster=preserved.KeywordCluster.ToList(),EvidenceSelections=selections,EstimatedWordCount=preserved.EstimatedWordCount,IsQualitySufficient=preserved.IsQualitySufficient};
    }
}
