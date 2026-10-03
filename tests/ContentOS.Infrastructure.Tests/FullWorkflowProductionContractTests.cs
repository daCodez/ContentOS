using ContentOS.Infrastructure.Workflow;
using NUnit.Framework;
using System.Text.Json.Nodes;
namespace ContentOS.Infrastructure.Tests;
public class FullWorkflowProductionContractTests
{
    private static JsonNode Source()=>JsonNode.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","RevisedIdeaWorkflow.json")))!;
    [Test] public void ReviewedIdeaContractSupportsConfigurableRubricWeights()
    {
        var source=Source();var dimensions=source["ScoringContract"]!["EditorialRubric"]!["Dimensions"]!.AsArray();
        dimensions[0]!["Weight"]=20;dimensions[1]!["Weight"]=25;
        Assert.DoesNotThrow(()=>FullWorkflowProductionContract.Validate(FullWorkflowSpecification.Parse(source.ToJsonString())));
    }
    [TestCase("unknownCheck")][TestCase("removedCheck")][TestCase("changedPolicy")][TestCase("unknownPolicy")][TestCase("unknownRootPolicy")][TestCase("automaticApproval")][TestCase("changedSequence")][TestCase("invalidRubric")]
    public void UnsupportedProductionContractCannotActivate(string defect)
    {
        var source=Source();var step=source["Actions"]![0]!["Steps"]![0]!;
        switch(defect)
        {
            case "unknownCheck":step["AcceptanceCheckKeys"]!.AsArray().Add("UnimplementedSemanticProof");break;
            case "removedCheck":step["AcceptanceCheckKeys"]!.AsArray().RemoveAt(0);break;
            case "changedPolicy":source["IdeaCountPolicy"]!["NoFiller"]=false;break;
            case "unknownPolicy":source["ExecutionSafeguards"]!["GuaranteeSourceTruth"]=true;break;
            case "unknownRootPolicy":source["NewRequiredPolicy"]=true;break;
            case "automaticApproval":source["FinalOutput"]!["Status"]="Approved";break;
            case "changedSequence":step["CapabilityKey"]="CaptureAudienceLanguage";break;
            case "invalidRubric":source["ScoringContract"]!["EditorialRubric"]!["Dimensions"]![0]!["Weight"]=99;break;
        }
        Assert.Throws<InvalidOperationException>(()=>FullWorkflowProductionContract.Validate(FullWorkflowSpecification.Parse(source.ToJsonString())));
    }
    [Test] public void MissingRequiredFieldsAndDuplicatePropertiesAreSafeValidationErrors()
    {
        var source=Source();source.AsObject().Remove("Version");Assert.Throws<InvalidOperationException>(()=>FullWorkflowSpecification.Parse(source.ToJsonString()));
        Assert.Throws<InvalidOperationException>(()=>FullWorkflowSpecification.Parse("{\"Id\":\"one\",\"id\":\"two\"}"));
    }
}
