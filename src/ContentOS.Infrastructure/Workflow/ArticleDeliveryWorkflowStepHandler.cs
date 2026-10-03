using ContentOS.Application.Research;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Articles;
using ContentOS.Infrastructure.Writing;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Research.Abstractions;
namespace ContentOS.Infrastructure.Workflow;

public sealed class ArticleDeliveryWorkflowState
{
    public Guid IdeaId {get;set;}
    public Guid SiteId {get;set;}
    public string ApprovedIdeaSnapshotHash {get;set;}="";
    public ContentIdea? Idea {get;set;}
    public List<string> SecondaryKeywords {get;set;}=[];
    public List<string> SourceSummaries {get;set;}=[];
    public SourceBoundArticleBriefResponse? SourceBoundBriefResponse {get;set;}
    public List<ArticleBriefEvidenceReference> BriefEvidenceReferences {get;set;}=[];
    public ContentBriefResult? Brief {get;set;}
    public ArticleOutlineResult? Outline {get;set;}
    public List<ArticleSearchObservation> SearchObservations {get;set;}=[];
    public List<ArticleGapJudgment> GapJudgments {get;set;}=[];
    public List<ArticleTermMapping> TermMappings {get;set;}=[];
    public ReaderEngagementPlan? EngagementPlan {get;set;}
    public List<ArticleVisualPlan> VisualPlan {get;set;}=[];
    public List<PublishedArticleTarget> InternalLinkInventory {get;set;}=[];
    public List<AppliedArticleLink> AppliedInternalLinks {get;set;}=[];
    public DateTime? RefreshReviewUtc {get;set;}
    public List<string> RefreshTriggers {get;set;}=[];
    public ArticleQaScorecard? Scorecard {get;set;}
    public GeneratedLongformArticle? Article {get;set;}
    public string Tone {get;set;}="";
    public List<string> RequiredSources {get;set;}=[];
    public string ApprovedAssetRoot {get;set;}="";
    public List<ArticleDeliveryImage> Images {get;set;}=[];
    public List<string> ContentVersionHistory {get;set;}=[];
    public ArticleVerificationReport? PostEditReview {get;set;}
    public FinalArticlePackage? Package {get;set;}
    public EditorialAssessmentResult? EditorialAssessment {get;set;}
    public string? ScoredPackageHash {get;set;}
    public bool AwaitingHumanApproval {get;set;}=true;
}
/// <summary>An explicitly authorized image backend; the generic paid image service is never an implicit fallback.</summary>
public interface IAuthorizedArticleAssetProvider
{
    Task<IReadOnlyList<ArticleDeliveryImage>> CreateAsync(GeneratedLongformArticle article,IReadOnlyList<ArticleVisualPlan> visualPlan,string approvedAssetRoot,CancellationToken cancellationToken);
}
public sealed class ArticleDeliveryWorkflowStepHandler(string capabilityKey,IWorkflowArticleWriter writer,
    IArticleEvidenceReviewer? reviewer=null,IAuthorizedArticleAssetProvider? assets=null):IFullWorkflowStepHandler
{
    public static readonly string[] Capabilities=["CraftRewriteArticle","HumanizeArticle","DetectRepetitionAndGenericAdvice","RecheckPostEditEvidence","CreateArticleImages","InsertArticleImages","BuildClickableToc","VerifyDeliveredArticlePackage"];
    public string CapabilityKey=>capabilityKey;
    public async Task<FullWorkflowStepResult> ExecuteAsync(FullWorkflowStepContext context,CancellationToken cancellationToken)
    {
        if(context.Specification.WorkflowType!=WorkflowDefinitionType.Article||context.PreviousOutput is null)throw new InvalidOperationException("Article delivery requires persisted upstream article state.");
        var state=context.PreviousOutput.Value.Deserialize<ArticleDeliveryWorkflowState>()??throw new InvalidOperationException("Missing article state.");
        var article=state.Article??throw new InvalidOperationException("Missing authoritative structured article.");
        var checks=new Dictionary<string,StepAcceptanceEvidence>(StringComparer.Ordinal);
        void Check(string key,string reason)=>checks.Add(key,new(true,reason,["step://"+context.StepRunId,"sha256:"+EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(state.Article!))]));
        switch(CapabilityKey)
        {
            case "CraftRewriteArticle":case "HumanizeArticle":
                var revision=await new EditorialRevisionService(writer).ReviseAsync(article,state.Tone,context.Step.Instructions,cancellationToken);
                state.ContentVersionHistory.Add(revision.InputHash);state.ContentVersionHistory.Add(revision.OutputHash);state.Article=revision.Article;
                // Every edit invalidates previous facts, score and assets tied to earlier content.
                state.PostEditReview=null;state.Package=null;state.EditorialAssessment=null;state.ScoredPackageHash=null;state.Images=[];
                Check("ProseActuallyChanged","The writer returned changed structured prose and a new content hash.");
                Check(CapabilityKey=="HumanizeArticle"?"FactsAndStructurePreserved":"RevisionPreservesMeaning","Numeric tokens, source URLs, headings, tables and list counts were preserved mechanically; semantic meaning still requires the post-edit evidence review.");
                if(CapabilityKey=="HumanizeArticle")Check("ToneApplied","Frozen tone was sent to the real revision request; prose and tone fit remain editorial judgments.");
                Check("NewContentVersionPersisted","The actual revised structured article and both version hashes are returned for atomic step persistence.");break;
            case "DetectRepetitionAndGenericAdvice":
                var paragraphs=article.IntroParagraphs.Concat(article.Sections.SelectMany(s=>s.Paragraphs)).Concat(article.ConclusionParagraphs).ToArray();
                var repeats=paragraphs.Where(p=>p.Length>=60).GroupBy(p=>Regex.Replace(p.Trim(),@"\s+"," "),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();
                if(repeats.Length>0||paragraphs.Any(p=>Regex.IsMatch(p,@"a useful longform article|this (?:section|article) should|the article should",RegexOptions.IgnoreCase)))throw new InvalidOperationException("Repeated or meta-writing passages require a real prose revision before delivery.");
                Check("ActualDraftInspected","Inspected paragraphs from the actual structured version.");Check("RepeatedPassagesLocated","No repeated long paragraphs or known meta-writing patterns found; this bounded check does not assess all generic advice.");break;
            case "RecheckPostEditEvidence":
                if(reviewer is null)throw new InvalidOperationException("Post-edit delivery blocked: no configured evidence reviewer capable of claim/source review and fresh link checks.");
                state.PostEditReview=await new ArticlePostEditVerificationService(reviewer).VerifyAsync(article,state.RequiredSources,context.FixtureMode,cancellationToken);
                Check("FinalChangedVersionInspected","Review is bound to the exact revised structured article hash.");Check("ClaimsAndSourcesRechecked","Explicit material-claim inventory, collected excerpts and review reasons retained.");
                Check("LinksRechecked","Fresh successful link records cover every final URL.");Check("UnresolvedFailuresBlockCompletion","All reported unresolved factual/link/arithmetic failures must be absent.");break;
            case "CreateArticleImages":
                if(assets is null)throw new InvalidOperationException("Image generation blocked: no explicitly authorized article image provider is configured. Paid image services are not a fallback.");
                state.Images=(await assets.CreateAsync(article,state.VisualPlan,state.ApprovedAssetRoot,cancellationToken)).ToList();
                state.Package=await FinalArticleDelivery.BuildAsync(article,state.Images,state.ApprovedAssetRoot,state.RequiredSources,cancellationToken);
                if(state.Package.IsFixture&&!context.FixtureMode)throw new InvalidOperationException("Fixture images cannot satisfy production delivery.");
                Check("AuthorizedProviderAvailable","An explicitly configured article asset provider was called.");Check("RealAssetFilesReadable","Actual image bytes validated.");Check("AssetIdentityAndProvenancePersisted","Hashes, article version, provider and alt text retained.");Check("NoRemotePlaceholderSuccess","Missing, stale or placeholder assets cannot form a package.");break;
            case "InsertArticleImages":case "BuildClickableToc":case "VerifyDeliveredArticlePackage":
                state.Package=await FinalArticleDelivery.BuildAsync(article,state.Images,state.ApprovedAssetRoot,state.RequiredSources,cancellationToken);
                if(state.Package.IsFixture&&!context.FixtureMode)throw new InvalidOperationException("Fixture exports cannot satisfy production delivery.");
                if(CapabilityKey=="InsertArticleImages")
                {Check("AssetsInsertedInAuthoritativeVersion","Verified bytes embedded in both exact exports.");Check("ReferencesResolve","Images are self-contained data URLs.");Check("AltTextPresent","Every image has nonempty alt text.");Check("ExportsRetainImages","Both HTML and Markdown carry the actual image bytes.");}
                else if(CapabilityKey=="BuildClickableToc")
                {Check("FinalHeadingsUsed","TOC uses the actual final section headings.");Check("UniqueAnchors","Duplicate headings get distinct anchors.");Check("EveryTocLinkResolves","Every TOC entry has an explicit target in both exports.");Check("ExportsRetainToc","Both exact exports contain the TOC.");}
                else
                {
                    if(state.PostEditReview is null)throw new InvalidOperationException("Exact delivery lacks fresh post-edit fact and link review.");
                    ArticlePostEditVerificationService.Validate(article,state.RequiredSources,state.PostEditReview,context.FixtureMode);
                    if(state.ScoredPackageHash!=state.Package.PackageHash)
                        throw new InvalidOperationException("Exact package needs a valid explained 0-100 editorial assessment at least 85; hard blockers override the score.");
                    ValidateEditorialAssessment(state.EditorialAssessment,context.Specification.Root.GetProperty("ScoringContract").GetProperty("EditorialRubric").Deserialize<EditorialRubric>()??throw new InvalidOperationException("Missing article rubric."),state.RequiredSources);
                    Check("ExactDeliveredVersionAndHashesRecorded","Article, HTML, Markdown and package hashes retained.");Check("AllRequiredOutputsPresent","Actual exports, sources, images, current post-edit evidence and explained score retained.");
                    Check("ClickableTocAndImagesRetained","The exact delivered exports retain both.");Check("NoPlaceholdersOrDuplicateFiller","Asset and prose checks passed.");
                    if(context.Specification.Root.TryGetProperty("ReaderEngagementPolicy",out _))
                    {
                        var engagement=state.EngagementPlan??throw new InvalidOperationException("Final delivery lost engagement plan.");
                        if(engagement.InteractionMode=="EvidenceReveal")
                        {var section=article.Sections.Single(s=>s.Heading==engagement.SectionHeading);if(!state.Package.Html.Contains("<details>")||!state.Package.Html.Contains("<summary>Reveal clue 1</summary>")||state.Package.Html.Contains("<script",StringComparison.OrdinalIgnoreCase)||section.Paragraphs.Any(p=>!state.Package.Markdown.Contains(p,StringComparison.Ordinal)))throw new InvalidOperationException("Actual native interaction or full static equivalent is missing.");}
                        else if(engagement.InteractionMode!="Static"||string.IsNullOrWhiteSpace(engagement.StaticFallbackReason))throw new InvalidOperationException("No documented static fallback.");
                        Check("InteractionAndStaticEquivalentRetained","Native keyboard-capable disclosures and complete Markdown text retained, or specific static fallback recorded. Rendering still requires browser verification.");
                    }
                    Check("HumanApprovalRequiredForCompletion","Output remains awaiting explicit human approval of this exact package; no publication or auto-approval.");
                }
                break;
            default:throw new InvalidOperationException("Unsupported article delivery step.");
        }
        return new(JsonSerializer.SerializeToElement(state),checks,context.FixtureMode||state.Package?.IsFixture==true||state.PostEditReview?.IsFixture==true);
    }
    public static void ValidateEditorialAssessment(EditorialAssessmentResult? assessment,EditorialRubric rubric,IReadOnlyCollection<string> collectedReferences)
    {
        if(assessment?.Score is not decimal score||score<85||score>100||assessment.Status!="AssessedModelOpinion")throw new InvalidOperationException("Article score must be an explained rubric assessment between 85 and 100.");
        var validated=EditorialRubricEvaluator.Evaluate(new(){RubricVersion=assessment.RubricVersion,Dimensions=assessment.Dimensions.ToList()},rubric,collectedReferences);
        if(validated.Status!="AssessedModelOpinion"||validated.Score!=score)throw new InvalidOperationException("Saved score does not match its validated rubric ratings and evidence.");
    }
}
