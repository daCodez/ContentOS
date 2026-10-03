using ContentOS.Application.Research;
using ContentOS.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ContentOS.Infrastructure.Workflow;

/// <summary>Rejects activation when a policy or acceptance contract has no implemented production support.</summary>
public static class FullWorkflowProductionContract
{
    private const string SupportedIdeaContract = """
{
  "Policies": {
    "RequiresHumanApproval": true,
    "FinalOutput": {
      "ArtifactType": "IdeaApprovalPackage",
      "Status": "PendingApproval",
      "LaunchesWorkflowType": "Article",
      "RequiredFields": [
        "ideas",
        "targetCount",
        "returnedCount",
        "shortfallReason",
        "collectedSourceReferences",
        "metricProvenance",
        "approvalStatus"
      ]
    },
    "ExecutionSafeguards": {
      "PersistActualStepOutput": true,
      "AcceptanceChecksRequired": true,
      "RejectSimulationAndLabelOnlyCompletion": true,
      "RetainInputAndOutputVersions": true,
      "RetainSourceAndAssetProvenance": true,
      "UnsupportedCapabilitiesBlockExecution": true,
      "FixtureOutputsCannotBecomePublishReady": true
    },
    "MetricPolicy": {
      "EditorialJudgments": [
        "pain",
        "clarity",
        "uniqueness",
        "usefulness",
        "priority",
        "seoPotential",
        "rankability",
        "monetizationFit"
      ],
      "MeasuredSeoMetrics": [
        "searchVolume",
        "rankingDifficulty",
        "rankingPosition",
        "demandTrend"
      ],
      "RequireMeasurementEvidence": true,
      "MeasurementEvidenceFields": [
        "provider",
        "collectedUtc",
        "query",
        "metricName",
        "value",
        "units",
        "sourceReference"
      ],
      "MissingMeasurementValue": null,
      "MissingMeasurementStatus": "Unknown",
      "EditorialJudgmentIsNotMeasuredSeoEvidence": true
    },
    "IdeaCountPolicy": {
      "TargetMinimum": 20,
      "TargetMaximum": 30,
      "MinimumIsNotAQuota": true,
      "AllowFewerEvidenceSupportedIdeas": true,
      "ShortfallReasonRequired": true,
      "NoFiller": true
    },
    "ImportPolicy": {
      "Mode": "DraftOnly",
      "ActivationRequiresExplicitPublish": true,
      "PreserveSourceIdentifiers": true,
      "PreserveHistoricalVersionsAndRuns": true
    },
    "ScoringContract": {
      "ContractVersion": "scoring-separation-v1",
      "AutomaticTechnicalChecks": {
        "Result": "PassFailUnknown",
        "RequireEvidence": true,
        "ContributeToEditorialScore": false,
        "ChecksAreNotIndependentEditorialJudgment": true
      },
      "EditorialRubric": {
        "ImplementationProposal": true,
        "WeightsSelectedByUser": false,
        "Configurable": true,
        "RatingScale": {
          "Minimum": 0,
          "Maximum": 4,
          "Anchors": {
            "0": "Absent or contradicted; fails reader need.",
            "1": "Weak or vague; major revision required.",
            "2": "Partially useful; material gaps remain.",
            "3": "Specific and grounded; minor limitations identified.",
            "4": "Excellent against this dimension; concrete evidence and clear reader value."
          }
        },
        "RequiredAssessmentFields": [
          "rubricVersion",
          "dimensions.key",
          "dimensions.rating",
          "dimensions.reason",
          "dimensions.evidenceReferences"
        ],
        "Normalization": "round(sum(rating / 4 * weight), 1); weights must total 100",
        "MissingOrInvalidAssessment": {
          "Status": "UnassessedOrInvalidAssessment",
          "Value": null,
          "DoNotSubstituteZero": true
        },
        "AssessorKindRequired": true,
        "GeneratorSelfAssessmentIsNotIndependentReview": true,
        "EvidenceReferencePresenceDoesNotVerifyRelevance": true
      },
      "SeoAssessment": {
        "EditorialSuggestionsRequireReasons": true,
        "MeasuredMetricsRequireProviderQueryDateValueUnitsSource": true,
        "UnavailableMeasuredValue": null,
        "UnavailableMeasuredStatus": "Unknown",
        "NoInventedSearchDemandDifficultyOrTrend": true
      },
      "CompletionGate": {
        "ArticlePassThreshold": null,
        "HardBlockersOverrideAggregate": true,
        "HumanApprovalRequired": true,
        "NoAutomaticApprovalOrPublishing": true
      },
      "HistoricalRecords": {
        "PreserveOriginalScoreAndScale": true,
        "MassRescoring": false,
        "RevisedScoreRequiresNewAssessmentVersion": true
      }
    }
  },
  "Steps": [
    {
      "CapabilityKey": "ResearchPainPoints",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "CollectedEvidencePresent",
        "PainPointsTraceToEvidence"
      ]
    },
    {
      "CapabilityKey": "CaptureAudienceLanguage",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "ObservedPhrasesTraceToCollectedExcerpts"
      ]
    },
    {
      "CapabilityKey": "GenerateIdeaCandidates",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "CandidatesRetainSupportingEvidence",
        "NoFillerToMeetQuota"
      ]
    },
    {
      "CapabilityKey": "FindPrimaryKeywords",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "ExplicitPrimaryKeywordPerIdea",
        "MetricProvenancePresent"
      ]
    },
    {
      "CapabilityKey": "BuildKeywordCluster",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "IntentContinuity",
        "NaturalSupportingPhrases",
        "MeasuredMetricsOrUnknown"
      ]
    },
    {
      "CapabilityKey": "ScoreIdeaQuality",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "EditorialScoreLabelled",
        "MeasurementEvidenceOrUnknown"
      ]
    },
    {
      "CapabilityKey": "ScoreMonetizationFit",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "NoInventedOffers",
        "JudgmentLabelled"
      ]
    },
    {
      "CapabilityKey": "FilterDuplicates",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "ExistingLedgerCompared",
        "DuplicateDecisionsPersisted"
      ]
    },
    {
      "CapabilityKey": "SaveIdeaRecords",
      "AcceptanceCheckKeys": [
        "OutputPresent",
        "OutputArtifactPersisted",
        "InputVersionMatched",
        "NotSimulated",
        "PendingApprovalRecordsPersisted",
        "ApprovalPackageComplete"
      ]
    }
  ]
}
""";
    public static void Validate(FullWorkflowSpecification specification,IFullWorkflowProductionAuthorization? authorization=null)
    {
        if(specification.WorkflowType!=WorkflowDefinitionType.Idea)
        {
            if(authorization is null)throw new InvalidOperationException("Article production requires explicitly configured scoped authorization and verified dependencies.");
            authorization.ValidateSpecification(specification);return;
        }
        var supported=JsonNode.Parse(SupportedIdeaContract)!;
        var root=JsonNode.Parse(specification.Root.GetRawText())!.AsObject();
        var revisedRanking=root["ScoringContract"]?["ContractVersion"]?.GetValue<string>()==IdeaRankingPolicy.ContractVersion;
        if(revisedRanking)
        {
            var ranking=root["ScoringContract"]?["RankingPolicy"]?.Deserialize<IdeaRankingPolicy>()??throw new InvalidOperationException("Ranking policy is required.");
            try { IdeaRankingEvaluator.Evaluate(null,ranking,new Dictionary<string,string>()); }
            catch(ArgumentException ex){throw new InvalidOperationException("Invalid ranking policy.",ex);}
            root["ScoringContract"]!.AsObject().Remove("RankingPolicy");
            root["ScoringContract"]!["ContractVersion"]="scoring-separation-v1";
            var quality=supported["Steps"]!.AsArray().Single(s=>s!["CapabilityKey"]!.GetValue<string>()=="ScoreIdeaQuality")!;
            quality["AcceptanceCheckKeys"]!.AsArray().Add("SeparateReviewPersisted");
            quality["AcceptanceCheckKeys"]!.AsArray().Add("ProvisionalRankingRetained");
        }
        var supportedRootKeys=new HashSet<string>(["Id","WorkflowDefinitionFamilyId","WorkflowType","Name","Description","Version","IsActive","RequiresHumanApproval","Actions","FinalOutput","ExecutionSafeguards","MetricPolicy","IdeaCountPolicy","ImportPolicy","ScoringContract"],StringComparer.Ordinal);
        if(root.Any(property=>!supportedRootKeys.Contains(property.Key)))throw new InvalidOperationException("Unknown production policy/property must remain an inactive draft until its runtime support is verified.");
        var rubric=root["ScoringContract"]?["EditorialRubric"]?.Deserialize<EditorialRubric>()
            ??throw new InvalidOperationException("Production activation requires a configured editorial rubric.");
        try { EditorialRubricEvaluator.Evaluate(null,rubric,[]); }
        catch(ArgumentException ex) { throw new InvalidOperationException("Invalid configured editorial rubric.",ex); }
        root["ScoringContract"]!["EditorialRubric"]!.AsObject().Remove("Dimensions");
        root["ScoringContract"]!["EditorialRubric"]!.AsObject().Remove("Version");
        foreach(var policy in supported["Policies"]!.AsObject())
            if(!JsonNode.DeepEquals(root[policy.Key],policy.Value))
                throw new InvalidOperationException("Unsupported production policy contract: "+policy.Key+". Save as an inactive draft until implementation and tests support this change.");
        var actual=specification.Actions.SelectMany(a=>a.Steps).ToArray();
        var expected=supported["Steps"]!.AsArray();
        if(actual.Length!=expected.Count)throw new InvalidOperationException("Production ideation requires the complete verified nine-step sequence.");
        for(var i=0;i<actual.Length;i++)
        {
            var step=expected[i]!;
            if(actual[i].CapabilityKey!=step["CapabilityKey"]!.GetValue<string>()||
                !actual[i].AcceptanceCheckKeys.ToHashSet(StringComparer.Ordinal).SetEquals(step["AcceptanceCheckKeys"]!.AsArray().Select(x=>x!.GetValue<string>())))
                throw new InvalidOperationException("Unsupported production step order or acceptance contract: "+actual[i].CapabilityKey);
        }
    }
}
