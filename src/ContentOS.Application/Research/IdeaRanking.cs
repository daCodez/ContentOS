namespace ContentOS.Application.Research;

/// <summary>Opt-in contract. Weights are proposed priorities, not measured probabilities.</summary>
public sealed class IdeaRankingPolicy
{
    public const string ContractVersion="idea-independent-ranking-v2";
    public string Version {get;set;}=ContractVersion;
    public Dictionary<string,decimal> Weights {get;set;}=new(DefaultWeights,StringComparer.Ordinal);
    public static Dictionary<string,decimal> DefaultWeights => new() { ["readerProblem"]=10,["audienceFit"]=15,["usefulness"]=15,["readerEngagement"]=10,["differentiation"]=10,["intentFit"]=10,["demand"]=15,["competition"]=10,["trend"]=5 };
}
public sealed record IdeaReviewQuote(string SourceUrl,string ExactQuote);
public sealed class IdeaReviewDimension
{
    public string Key {get;set;}="";
    public decimal? Rating {get;set;}
    public string Reason {get;set;}="";
    public List<IdeaReviewQuote> Evidence {get;set;}=[];
}
public sealed class IdeaReviewResponse
{
    public string Version {get;set;}=IdeaRankingPolicy.ContractVersion;
    public List<IdeaReviewDimension> Dimensions {get;set;}=[];
}
public sealed record IdeaRankingResult(string Status,decimal? OverallScore,decimal? LowerBound,decimal? UpperBound,decimal KnownWeight,
    string Version,IReadOnlyList<IdeaReviewDimension> Dimensions,IReadOnlyDictionary<string,decimal> Weights,IReadOnlyList<string> Problems)
{
    public string AssessorKind => "SeparateEditorialReviewCall";
    public bool IndependentReview => true;
    public bool IndependentProvider => false;
    public string RankingBasis => "Descending evidenced lower bound; overlapping ranges do not establish a winner";
}
public static class IdeaRankingEvaluator
{
    public static IdeaRankingResult Evaluate(IdeaReviewResponse? response,IdeaRankingPolicy policy,IReadOnlyDictionary<string,string> sources)
    {
        if(policy.Version!=IdeaRankingPolicy.ContractVersion||policy.Weights is null||!policy.Weights.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(IdeaRankingPolicy.DefaultWeights.Keys)||policy.Weights.Values.Any(w=>w<=0)||policy.Weights.Values.Sum()!=100)
            throw new ArgumentException("Ranking requires the supported version and positive weights for all required dimensions totaling 100.");
        IdeaRankingResult Result(string status,decimal? lower,decimal? upper,decimal known,IReadOnlyList<string> problems)=>new(status,status=="Reviewed"?lower:null,lower,upper,known,policy.Version,response?.Dimensions??[],new Dictionary<string,decimal>(policy.Weights),problems);
        if(response is null)return Result("Unassessed",null,null,0,["A separate review was not returned; generator scores are not ranking evidence."]);
        var problems=new List<string>();var dimensions=response.Dimensions;
        if(response.Version!=policy.Version||dimensions is null||dimensions.Any(d=>d is null)||dimensions.Count!=policy.Weights.Count||!dimensions.Select(d=>d.Key).ToHashSet(StringComparer.Ordinal).SetEquals(policy.Weights.Keys))return Result("InvalidReview",null,null,0,["Review version or required dimensions differ from the ranking contract."]);
        foreach(var d in dimensions)
        {
            if(d.Evidence is null)problems.Add("Evidence list must be present, even when empty: "+d.Key);
            if(string.IsNullOrWhiteSpace(d.Reason)||d.Reason.Length<30)problems.Add("Specific reason required: "+d.Key);
            if(d.Rating is <0 or >4)problems.Add("Rating outside 0-4: "+d.Key);
            if(d.Key is "demand" or "competition" or "trend")
            {
                if(d.Rating.HasValue)problems.Add("No verified market observation provider is configured: "+d.Key);
            }
            else if(!d.Rating.HasValue)problems.Add("Editorial review is incomplete: "+d.Key);
            if(d.Rating.HasValue&&(d.Evidence is null||d.Evidence.Count==0))problems.Add("Exact excerpt citation required: "+d.Key);
            foreach(var quote in d.Evidence??[])
                if(quote is null||string.IsNullOrWhiteSpace(quote.SourceUrl)||!Uri.TryCreate(quote.SourceUrl,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo)||string.IsNullOrWhiteSpace(quote.ExactQuote)||quote.ExactQuote.Length<12||!sources.TryGetValue(quote.SourceUrl,out var text)||!text.Contains(quote.ExactQuote,StringComparison.Ordinal))problems.Add("Uncollected or unsafe URL or nonverbatim quote: "+d.Key);
        }
        if(problems.Count>0)return Result("InvalidReview",null,null,0,problems);
        var known=dimensions.Where(d=>d.Rating.HasValue).Sum(d=>policy.Weights[d.Key]);
        var lower=Math.Round(dimensions.Where(d=>d.Rating.HasValue).Sum(d=>d.Rating!.Value/4m*policy.Weights[d.Key]),2);
        return Result(known==100?"Reviewed":"Provisional",lower,Math.Round(lower+100-known,2),known,[]);
    }
}
