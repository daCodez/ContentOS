using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ContentOS.Infrastructure.Scoring;

namespace ContentOS.Infrastructure.Workflow;

public sealed class IdeaWorkflowState
{
    public ResearchContext Context { get; set; }=new();
    public List<ResearchFinding> Findings { get; set; }=[];
    public List<ResearchProviderCollection> ProviderCollections { get; set; }=[];
    public Dictionary<string,string> CandidateIntents { get; set; }=new(StringComparer.Ordinal);
    public List<CandidateContentIdea> Candidates { get; set; }=[];
    public List<AudienceEvidenceQuote> AudienceQuotes { get; set; }=[];
    public List<KeywordSearchEvidence> KeywordSearches { get; set; }=[];
    public List<IdeaDuplicateDecision> DuplicateDecisions { get; set; }=[];
    public List<Guid> SavedIdeaIds { get; set; }=[];
    public int TargetCount { get; set; }
    public string? ShortfallReason { get; set; }
    public string MeasuredSeoStatus { get; set; }="Unknown";
    public decimal? SearchVolume { get; set; }
    public decimal? RankingDifficulty { get; set; }
    public decimal? DemandTrend { get; set; }
    public string MonetizationAssessmentStatus { get; set; }="UnassessedNoVerifiedInventory";
    public string MonetizationReason { get; set; }="No verified offer inventory was supplied; no offers, downloads or affiliate promises are inferred.";
    public bool LedgerCompared { get; set; }
    public bool FixtureEvidence { get; set; }
    public string ApprovalStatus { get; set; }="PendingApproval";
}
public sealed record AudienceEvidenceQuote(string Quote,string SourceUrl,string Limitation);
public sealed record ResearchProviderCollection(string Provider,DateTime CollectedUtc,int ReturnedCount,int UsableCount,string Outcome);
public sealed record KeywordSearchEvidence(string Keyword,DateTime CollectedUtc,IReadOnlyList<SearchResult> Results,string MeasurementStatus);
public sealed record IdeaDuplicateDecision(string Title,bool Duplicate,string Reason);

/// <summary>Existing research and ideation services executed as real, separately persisted steps.</summary>
public sealed class IdeaWorkflowStepHandler(string capabilityKey,ContentOsDbContext db,IEnumerable<IResearchSourceProvider> providers,
    IIdeationAgent ideation,IResearchSearchClient search,IIdeaDeduplicator deduplicator,ISeparateIdeaReviewer? reviewer=null):IFullWorkflowStepHandler
{
    public static readonly string[] Capabilities=["ResearchPainPoints","CaptureAudienceLanguage","GenerateIdeaCandidates","FindPrimaryKeywords","BuildKeywordCluster","ScoreIdeaQuality","ScoreMonetizationFit","FilterDuplicates","SaveIdeaRecords"];
    public string CapabilityKey=>capabilityKey;
    public async Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext execution,CancellationToken cancellationToken)
    {
        if(execution.Specification.WorkflowType!=WorkflowDefinitionType.Idea)throw new InvalidOperationException("Idea handler cannot execute article steps.");
        var state=execution.PreviousOutput.HasValue?execution.PreviousOutput.Value.Deserialize<IdeaWorkflowState>()??throw new InvalidOperationException("Missing persisted idea state."):new IdeaWorkflowState();
        var checks=new Dictionary<string,StepAcceptanceEvidence>(StringComparer.Ordinal);
        void Check(string key,bool pass,string reason,IEnumerable<string>? references=null)=>checks.Add(key,new(pass,reason,(references??["step://"+execution.StepRunId]).ToArray()));
        switch(CapabilityKey)
        {
            case "ResearchPainPoints":
                if(!execution.FrozenRunInput.TryGetProperty("siteId",out var siteId)||!Guid.TryParse(siteId.GetString(),out var id))throw new InvalidOperationException("Explicit site ID is required.");
                var site=await db.Sites.SingleOrDefaultAsync(s=>s.Id==id&&s.IsActive,cancellationToken)??throw new InvalidOperationException("Site is missing or inactive.");
                if(!execution.FrozenRunInput.TryGetProperty("audienceDescription",out var audience)||string.IsNullOrWhiteSpace(audience.GetString()))throw new InvalidOperationException("An explicit reader audience is required; tone is not an audience.");
                var seeds=execution.FrozenRunInput.TryGetProperty("seedTopics",out var seedList)?seedList.EnumerateArray().Select(x=>x.GetString()??"").Where(x=>!string.IsNullOrWhiteSpace(x)).Take(8).ToList():[];
                if(seeds.Count==0)throw new InvalidOperationException("Explicit research seed topics are required.");
                state.TargetCount=execution.FrozenRunInput.TryGetProperty("targetIdeaCount",out var count)?Math.Clamp(count.GetInt32(),20,30):25;
                state.Context=new(){SiteId=site.Id,SiteName=site.Name,SiteUrl=site.Domain,Niche=site.Niche,AudienceDescription=audience.GetString()!,SeedTopics=seeds,MaxIdeasToSave=state.TargetCount,MaxFindingsPerProvider=4};
                state.Context.VerifiedProductContext=execution.FrozenRunInput.TryGetProperty("verifiedProductContext",out var productContext)?productContext.GetString()??"":"";
                if(execution.Specification.Root.GetProperty("ScoringContract").TryGetProperty("EditorialRubric",out var rubric))
                    state.Context.IdeaEditorialRubric=rubric.Deserialize<EditorialRubric>()??throw new InvalidOperationException("Invalid configured editorial rubric.");
                var sourceProviders=providers.ToArray();if(sourceProviders.Length==0)throw new InvalidOperationException("Real research providers are unavailable.");
                foreach(var provider in sourceProviders)
                {
                    var actual=await provider.ResearchAsync(state.Context,cancellationToken);
                    state.FixtureEvidence|=actual.Any(f=>f.SourceType.Contains("Fixture",StringComparison.OrdinalIgnoreCase));
                    var usable=actual.Where(f=>Uri.TryCreate(f.SourceUrl,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https"&&!string.IsNullOrWhiteSpace(ResearchEvidenceHandoff.CleanExcerpt(f.SourceExcerpt))).ToArray();
                    state.ProviderCollections.Add(new(provider.GetType().Name,DateTime.UtcNow,actual.Count,usable.Length,usable.Length==0?"NoUsableEvidenceReturned":"CollectedEvidence; relevance remains unverified"));
                    state.Findings.AddRange(usable);
                }
                if(state.FixtureEvidence&&!execution.FixtureMode)throw new InvalidOperationException("Fixture research cannot satisfy production ideation.");
                state.Findings=state.Findings.OrderByDescending(f=>f.SourceType=="SiteOwnedContext").DistinctBy(f=>f.SourceUrl).Take(100).ToList();
                if(state.Findings.Count==0)throw new InvalidOperationException("No usable real source excerpts were collected; ideation is blocked.");
                Check("CollectedEvidencePresent",true,"Provider-collected URLs and excerpts retained; source relevance remains reviewable, not verified.",state.Findings.Select(f=>f.SourceUrl));
                Check("PainPointsTraceToEvidence",true,"Observed pain-point evidence remains linked to the collected excerpt; inferred audience judgments are separately labeled.",state.Findings.Select(f=>f.SourceUrl));
                break;
            case "CaptureAudienceLanguage":
                state.AudienceQuotes=state.Findings.Select(f=>new AudienceEvidenceQuote(ResearchEvidenceHandoff.CleanExcerpt(f.SourceExcerpt),f.SourceUrl,"Collected excerpt, possibly a snippet; audience identity and relevance require review.")).ToList();
                Check("ObservedPhrasesTraceToCollectedExcerpts",state.AudienceQuotes.Count>0,"Quotes are actual cleaned excerpts, not generated audience language.",state.AudienceQuotes.Select(q=>q.SourceUrl));break;
            case "GenerateIdeaCandidates":
                state.Context.ApprovedWorkflowInstructions=execution.Step.Instructions;
                state.Candidates=(await ideation.GenerateIdeasAsync(state.Context,state.Findings,cancellationToken)).Where(i=>i.SupportingFindings.Count>0).Take(state.TargetCount).ToList();
                state.ShortfallReason=Shortfall(state);
                state.CandidateIntents=state.Candidates.GroupBy(c=>c.Title,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().SearchIntent,StringComparer.Ordinal);
                Check("CandidatesRetainSupportingEvidence",state.Candidates.All(i=>i.SupportingFindings.Count>0),"Every retained candidate selects collected source evidence; zero supported candidates is allowed with a shortfall reason.");
                Check("NoFillerToMeetQuota",state.Candidates.Count<=state.TargetCount&&(state.Candidates.Count==state.TargetCount||!string.IsNullOrWhiteSpace(state.ShortfallReason)),"Evidence-constrained candidates; target is not a quota.");break;
            case "FindPrimaryKeywords":
                foreach(var keyword in state.Candidates.Select(i=>i.PrimaryKeyword).Distinct(StringComparer.Ordinal))
                {
                    if(string.IsNullOrWhiteSpace(keyword))throw new InvalidOperationException("Candidate is missing its primary keyword.");
                    var results=await search.SearchAsync(keyword,maxResults:5,cancellationToken:cancellationToken);
                    state.KeywordSearches.Add(new(keyword,DateTime.UtcNow,results.ToArray(),"Unknown: web search is not a measured keyword metrics provider"));
                }
                Check("ExplicitPrimaryKeywordPerIdea",state.Candidates.All(i=>!string.IsNullOrWhiteSpace(i.PrimaryKeyword)),"Actual candidate keywords were queried through the existing search provider.");
                Check("MetricProvenancePresent",state.KeywordSearches.Count==state.Candidates.Select(i=>i.PrimaryKeyword).Distinct().Count(),"Query/date/results retained; measured volume/difficulty/trends remain explicitly null/unknown.");break;
            case "BuildKeywordCluster":
                foreach(var candidate in state.Candidates)candidate.SecondaryKeywords=candidate.SecondaryKeywords.Where(s=>!string.IsNullOrWhiteSpace(s)&&s.Length<=240&&!s.Contains("http",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
                Check("IntentContinuity",state.Candidates.All(i=>!string.IsNullOrWhiteSpace(i.SearchIntent)&&state.CandidateIntents.TryGetValue(i.Title,out var original)&&original==i.SearchIntent),"Original candidate intent is unchanged through keyword grouping; semantic relevance still needs editorial review.");
                Check("NaturalSupportingPhrases",state.Candidates.All(i=>i.SecondaryKeywords.Count<=16&&i.SecondaryKeywords.Distinct(StringComparer.OrdinalIgnoreCase).Count()==i.SecondaryKeywords.Count&&i.SecondaryKeywords.All(s=>!string.IsNullOrWhiteSpace(s)&&s.Length<=240&&!s.Contains("http",StringComparison.OrdinalIgnoreCase))),"Model suggestions meet explicit phrase bounds and uniqueness checks; naturalness and topical fit remain unverified editorial judgments.");
                Check("MeasuredMetricsOrUnknown",state.SearchVolume is null&&state.RankingDifficulty is null&&state.DemandTrend is null,"No measured metrics provider: values are null and status Unknown.");break;
            case "ScoreIdeaQuality":
                if(execution.Specification.Root.GetProperty("ScoringContract").GetProperty("ContractVersion").GetString()==IdeaRankingPolicy.ContractVersion)
                {
                    var policy=execution.Specification.Root.GetProperty("ScoringContract").GetProperty("RankingPolicy").Deserialize<IdeaRankingPolicy>()??throw new InvalidOperationException("Ranking policy missing.");
                    if(reviewer is null)throw new InvalidOperationException("Separate reviewer unavailable; generator self-assessment cannot substitute.");
                    foreach(var candidate in state.Candidates)
                        candidate.ReviewedRanking=await reviewer.ReviewAsync(candidate,state.Context,state.KeywordSearches,policy,cancellationToken);
                    Check("SeparateReviewPersisted",state.Candidates.All(c=>c.ReviewedRanking is {Status:"Provisional" or "Reviewed"}),"Fresh review call excludes generator scores; exact quote provenance checked; semantic judgments remain model opinion.");
                    Check("ProvisionalRankingRetained",state.Candidates.All(c=>c.ReviewedRanking is {OverallScore:null,Status:"Provisional"}),"Unmeasured demand/competition/trend stay null. Overall range retains their possible contribution; lower bound orders provisionally.");
                }
                Check("EditorialScoreLabelled",state.Candidates.All(i=>i.EditorialAssessment is not null),"Versioned rubric ratings, reasons, source references and generator self-assessment limitations are retained; invalid or absent assessments remain unassessed.");
                Check("MeasurementEvidenceOrUnknown",state.MeasuredSeoStatus=="Unknown","Editorial quality is separate from unmeasured SEO demand/difficulty.");break;
            case "ScoreMonetizationFit":
                Check("NoInventedOffers",true,state.MonetizationReason);
                Check("JudgmentLabelled",state.MonetizationAssessmentStatus=="UnassessedNoVerifiedInventory","Optional monetization is unassessed without verified inventory; it does not penalize reader usefulness.");break;
            case "FilterDuplicates":
                var ledger=await db.ContentIdeas.Where(i=>i.SiteId==state.Context.SiteId&&i.Status!="Archived").AsNoTracking().ToListAsync(cancellationToken);
                var retained=new List<CandidateContentIdea>();state.DuplicateDecisions=[];
                foreach(var candidate in state.Candidates)
                {
                    var duplicate=deduplicator.IsDuplicate(candidate,ledger)||deduplicator.IsDuplicate(candidate,retained);
                    state.DuplicateDecisions.Add(new(candidate.Title,duplicate,duplicate?"Existing ledger or retained candidate matches topic/angle/intent/pain or near-identical title":"No duplicate under the current ledger comparison rules"));
                    if(!duplicate)retained.Add(candidate);
                }
                state.Candidates=retained;state.LedgerCompared=true;state.ShortfallReason=Shortfall(state);
                Check("ExistingLedgerCompared",true,$"Compared {ledger.Count} existing site ideas without rescoring or modifying them.");
                Check("DuplicateDecisionsPersisted",true,"Keep/drop reasons returned as part of the actual step output.");break;
            case "SaveIdeaRecords":
                if(!state.LedgerCompared)throw new InvalidOperationException("Ledger comparison must complete before saving ideas.");
                var run=await db.WorkflowDefinitionRuns.SingleAsync(r=>r.Id==execution.RunId,cancellationToken);
                var definition=await db.WorkflowDefinitions.SingleAsync(d=>d.Id==run.WorkflowDefinitionId,cancellationToken);
                state.SavedIdeaIds=[];
                foreach(var candidate in state.Candidates)
                {
                    var record=new IdeaRecord{Id=Guid.NewGuid(),SourceWorkflowRunId=run.Id,SourceWorkflowDefinitionId=definition.Id,WorkflowVersion=definition.Version,SiteId=state.Context.SiteId,IdeaTitle=candidate.Title,ReaderProblem=candidate.AudiencePainPoint,AudienceType=state.Context.AudienceDescription,SearchIntent=candidate.SearchIntent,EmotionalTrigger=candidate.WhyNow,UniquenessAngle=candidate.RecommendedAngle,PriorityScore=candidate.ReviewedRanking?.LowerBound??candidate.EditorialQualityScore??0m,Status=IdeaRecordStatus.Pending,
                        CanonicalTopic=deduplicator.ExtractCanonicalTopic(candidate.Title,candidate.PrimaryKeyword),Angle=deduplicator.ExtractAngle(candidate.Title,candidate.PrimaryKeyword,candidate.RecommendedAngle),Intent=deduplicator.ExtractIntent(candidate.SearchIntent),PainPoint=deduplicator.ExtractPainPoint(candidate.AudiencePainPoint),
                        IdeaSnapshotJson=JsonSerializer.Serialize(new{ideaTitle=candidate.Title,readerProblem=candidate.AudiencePainPoint,audienceType=state.Context.AudienceDescription,audienceGoal=candidate.AudienceGoal,searchIntent=candidate.SearchIntent,uniquenessAngle=candidate.RecommendedAngle,primaryKeyword=candidate.PrimaryKeyword,secondaryKeywordsJson=JsonSerializer.Serialize(candidate.SecondaryKeywords),sourceSummaryJson=JsonSerializer.Serialize(candidate.SupportingFindings.Select(ResearchEvidenceHandoff.ToWriterSummary)),summary=candidate.Summary,contentType=candidate.ContentType,reviewedRanking=candidate.ReviewedRanking,editorialQualityScore=candidate.EditorialQualityScore,editorialScoringStatus=candidate.EditorialScoringStatus,editorialAssessment=candidate.EditorialAssessment,editorialRubric=state.Context.IdeaEditorialRubric,measuredSeo=new{status="Unknown",searchVolume=(decimal?)null,rankingDifficulty=(decimal?)null,demandTrend=(decimal?)null},monetizationAssessmentStatus=state.MonetizationAssessmentStatus,sourceRelevanceVerified=false,fixture=execution.FixtureMode||state.FixtureEvidence,approvalStatus="PendingApproval"})};
                    db.IdeaRecords.Add(record);state.SavedIdeaIds.Add(record.Id);
                    db.ContentIdeas.Add(new(){Id=Guid.NewGuid(),SiteId=state.Context.SiteId,Title=candidate.Title,PrimaryKeyword=candidate.PrimaryKeyword,AudiencePainPoint=candidate.AudiencePainPoint,AudienceGoal=candidate.AudienceGoal,SearchIntent=candidate.SearchIntent,RecommendedAngle=candidate.RecommendedAngle,Status="Pending",OverallScore=candidate.ReviewedRanking?.LowerBound??candidate.EditorialQualityScore??0m,CanonicalTopic=record.CanonicalTopic,Angle=record.Angle,Intent=record.Intent,PainPoint=record.PainPoint,SourceSummaryJson=JsonSerializer.Serialize(candidate.SupportingFindings.Select(ResearchEvidenceHandoff.ToWriterSummary))});
                }
                // The runtime commits pending records and this step's output together; the handler does not commit early.
                state.ShortfallReason=Shortfall(state);
                Check("PendingApprovalRecordsPersisted",state.SavedIdeaIds.Count==state.Candidates.Count,"Pending records are staged in the same database transaction as completed step evidence; no article launch or approval.");
                Check("ApprovalPackageComplete",state.SavedIdeaIds.Count==state.TargetCount||!string.IsNullOrWhiteSpace(state.ShortfallReason),"Package retains candidate evidence, nullable editorial scores, rubric reasons, unknown SEO metrics and an explicit evidence-constrained shortfall.");break;
            default:throw new InvalidOperationException("Unsupported idea step: "+CapabilityKey);
        }
        return new(JsonSerializer.SerializeToElement(state),checks,execution.FixtureMode||state.FixtureEvidence);
    }
    private static string? Shortfall(IdeaWorkflowState state)=>state.Candidates.Count<state.TargetCount?$"Returned {state.Candidates.Count} of {state.TargetCount} target ideas from {state.Findings.Count} collected excerpts after source-support and duplicate checks. No filler was added. Source relevance and editorial assessments require human review.":null;
}
