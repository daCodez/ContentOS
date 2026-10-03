using ContentOS.Application.Research;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class EditorialRubricRegressionTests
{
    private static EditorialAssessmentResponse Assessment(decimal score) => new()
    {
        RubricVersion = EditorialRubric.IdeaProposal.Version,
        Dimensions = EditorialRubric.IdeaProposal.Dimensions.Select(d => new EditorialDimensionAssessment
        { Key = d.Key, Rating = score, Reason = "A specific editorial judgment grounded in the supplied reader problem.", EvidenceReferences = ["https://example.org/source"] }).ToList()
    };

    [Test]
    public void MissingAssessmentIsDifferentFromAssessedZero()
    {
        var missing = EditorialRubricEvaluator.Evaluate(null, EditorialRubric.IdeaProposal, ["https://example.org/source"]);
        var zero = EditorialRubricEvaluator.Evaluate(Assessment(0), EditorialRubric.IdeaProposal, ["https://example.org/source"]);
        Assert.That(missing.Score, Is.Null);
        Assert.That(missing.Status, Is.EqualTo("Unassessed"));
        Assert.That(zero.Score, Is.EqualTo(0));
        Assert.That(zero.Status, Is.EqualTo("AssessedModelOpinion"));
    }

    [Test]
    public void ConfigurableWeightsNormalizeToOneHundred()
    {
        var rubric = EditorialRubric.IdeaProposal;
        var assessment = Assessment(4);
        assessment.Dimensions.Single(x => x.Key == "readerProblem").Rating = 0;
        Assert.That(EditorialRubricEvaluator.Evaluate(assessment, rubric, ["https://example.org/source"]).Score, Is.EqualTo(75));
        Assert.That(EditorialRubricEvaluator.Evaluate(Assessment(4), rubric, ["https://example.org/source"]).Score, Is.EqualTo(100));
    }

    [TestCase("missingReason")]
    [TestCase("inventedSource")]
    [TestCase("missingDimension")]
    [TestCase("wrongVersion")]
    [TestCase("outOfRange")]
    [TestCase("missingRating")]
    [TestCase("duplicateDimension")]
    [TestCase("nullDimension")]
    public void InvalidJudgmentCannotBecomeAQualityScore(string defect)
    {
        var response = Assessment(3);
        switch (defect)
        {
            case "missingReason": response.Dimensions[0].Reason = ""; break;
            case "inventedSource": response.Dimensions[0].EvidenceReferences = ["https://invented.org/source"]; break;
            case "missingDimension": response.Dimensions.RemoveAt(0); break;
            case "wrongVersion": response.RubricVersion = "unknown"; break;
            case "outOfRange": response.Dimensions[0].Rating = 5; break;
            case "missingRating": response.Dimensions[0].Rating = null; break;
            case "duplicateDimension": response.Dimensions[1].Key = response.Dimensions[0].Key; break;
            case "nullDimension": response.Dimensions[0] = null!; break;
        }
        var result = EditorialRubricEvaluator.Evaluate(response, EditorialRubric.IdeaProposal, ["https://example.org/source"]);
        Assert.That(result.Score, Is.Null);
        Assert.That(result.Status, Is.EqualTo("InvalidAssessment"));
        Assert.That(result.Problems, Is.Not.Empty);
    }

    [Test]
    public void InvalidWeightConfigurationFailsBeforeScoring()
    {
        var rubric = EditorialRubric.IdeaProposal;
        rubric.Dimensions[0].Weight = 99;
        Assert.Throws<ArgumentException>(() => EditorialRubricEvaluator.Evaluate(Assessment(4), rubric, ["https://example.org/source"]));
    }

    [Test]
    public void HardBlockersOverridePerfectEditorialScore()
    {
        var result = EditorialRubricEvaluator.Evaluate(Assessment(4), EditorialRubric.IdeaProposal, ["https://example.org/source"]);
        Assert.That(EditorialRubricEvaluator.CanComplete(result, 85, ["MissingHumanApproval"]), Is.False);
        Assert.That(EditorialRubricEvaluator.CanComplete(result, 85, []), Is.True);
        Assert.That(EditorialRubricEvaluator.CanComplete(EditorialRubricEvaluator.Evaluate(null, EditorialRubric.IdeaProposal, []), 85, []), Is.False);
    }
}
