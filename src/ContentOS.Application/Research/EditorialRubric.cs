namespace ContentOS.Application.Research;

/// <summary>Versioned, configurable editorial judgments; these weights are implementation proposals.</summary>
public sealed class EditorialRubric
{
    public string Version { get; set; } = "";
    public bool ImplementationProposal { get; set; } = true;
    public List<EditorialRubricDimension> Dimensions { get; set; } = [];
    public Dictionary<int, string> RatingAnchors { get; set; } = new()
    {
        [0] = "Absent, contradicted, or fails the reader need; explain the defect.",
        [1] = "Weak: vague or unsupported, with major revision needed.",
        [2] = "Partial: some specific value, but material gaps remain.",
        [3] = "Strong: specific and grounded, with minor limitations identified.",
        [4] = "Excellent against this dimension: concrete evidence and clear reader value; explain why."
    };
    public static EditorialRubric IdeaProposal => new()
    {
        Version = "idea-editorial-proposal-v1",
        Dimensions = [new("readerProblem", 25), new("audienceFit", 20), new("usefulness", 25), new("differentiation", 20), new("clarity", 10)]
    };
    public static EditorialRubric ArticleProposal => new()
    {
        Version = "article-editorial-proposal-v1",
        Dimensions = [new("readerValue", 25), new("accuracyAndTrust", 25), new("clarityAndVoice", 20), new("structureAndCompleteness", 15), new("intentAndDifferentiation", 15)]
    };
}

public sealed class EditorialRubricDimension(string key, decimal weight)
{
    public string Key { get; set; } = key;
    public decimal Weight { get; set; } = weight;
}

public sealed class EditorialAssessmentResponse
{
    public string RubricVersion { get; set; } = "";
    public List<EditorialDimensionAssessment> Dimensions { get; set; } = [];
}

public sealed class EditorialDimensionAssessment
{
    public string Key { get; set; } = "";
    public decimal? Rating { get; set; }
    public string Reason { get; set; } = "";
    public List<string> EvidenceReferences { get; set; } = [];
}

public sealed record EditorialAssessmentResult(string Status, decimal? Score, string RubricVersion,
    IReadOnlyList<EditorialDimensionAssessment> Dimensions, IReadOnlyList<string> Problems)
{
    public string AssessorKind => "GeneratorSelfAssessment";
    public bool IndependentReview => false;
    public bool SourceRelevanceVerified => false;
}

/// <summary>Validates and aggregates explicit editorial judgments; never infers quality from populated fields.</summary>
public static class EditorialRubricEvaluator
{
    public static EditorialAssessmentResult Evaluate(EditorialAssessmentResponse? response, EditorialRubric rubric,
        IReadOnlyCollection<string> collectedEvidenceReferences)
    {
        if (string.IsNullOrWhiteSpace(rubric.Version) || rubric.Dimensions.Count == 0
            || rubric.Dimensions.Any(d => string.IsNullOrWhiteSpace(d.Key) || d.Weight <= 0)
            || rubric.Dimensions.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count() != rubric.Dimensions.Count
            || rubric.Dimensions.Sum(d => d.Weight) != 100)
            throw new ArgumentException("Editorial rubric requires a version, unique dimensions and positive weights totaling 100.", nameof(rubric));
        if (response is null)
            return new("Unassessed", null, rubric.Version, [], ["No rubric-backed assessment was returned."]);
        var problems = new List<string>();
        if (response.RubricVersion != rubric.Version) problems.Add("Assessment rubric version differs from the requested version.");
        var dimensions = response.Dimensions ?? [];
        if (dimensions.Any(d => d is null))
            return new("InvalidAssessment", null, rubric.Version, [], ["Null assessment dimension."]);
        if (dimensions.Count != rubric.Dimensions.Count || dimensions.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count() != dimensions.Count)
            problems.Add("Exactly one assessment per required dimension is required.");
        foreach (var expected in rubric.Dimensions)
        {
            var dimension = dimensions.FirstOrDefault(d => d.Key == expected.Key);
            if (dimension is null) { problems.Add($"Missing dimension: {expected.Key}."); continue; }
            if (dimension.Rating is null || dimension.Rating < 0 || dimension.Rating > 4) problems.Add($"Missing rating or rating outside 0–4: {expected.Key}.");
            if (string.IsNullOrWhiteSpace(dimension.Reason)) problems.Add($"Missing judgment reason: {expected.Key}.");
            if (dimension.EvidenceReferences is null || dimension.EvidenceReferences.Count == 0
                || dimension.EvidenceReferences.Any(e => string.IsNullOrWhiteSpace(e) || !collectedEvidenceReferences.Contains(e, StringComparer.Ordinal)))
                problems.Add($"Missing or uncollected evidence reference: {expected.Key}.");
        }
        if (dimensions.Any(d => !rubric.Dimensions.Any(expected => expected.Key == d.Key))) problems.Add("Unknown assessment dimension.");
        if (problems.Count > 0) return new("InvalidAssessment", null, rubric.Version, dimensions, problems);
        var score = Math.Round(rubric.Dimensions.Sum(d => dimensions.Single(x => x.Key == d.Key).Rating!.Value / 4m * d.Weight), 1);
        return new("AssessedModelOpinion", score, rubric.Version, dimensions, []);
    }

    public static bool CanComplete(EditorialAssessmentResult assessment, decimal threshold, IReadOnlyCollection<string> hardBlockers)
        => hardBlockers.Count == 0 && assessment.Status == "AssessedModelOpinion" && assessment.Score is >= 0
            && assessment.Score >= threshold;
}
