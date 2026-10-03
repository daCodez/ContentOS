using ContentOS.Infrastructure.Workflow;
using NUnit.Framework;
using System.Text.Json.Nodes;
namespace ContentOS.Infrastructure.Tests;
public class RankingActivationTests
{
    private static JsonNode Source()=>JsonNode.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"Fixtures","IndependentRankingWorkflow.json")))!;
    [Test] public void ReviewedRankingContractCanBeExplicitlyActivated()
    {Assert.DoesNotThrow(()=>FullWorkflowProductionContract.Validate(FullWorkflowSpecification.Parse(Source().ToJsonString())));}
    [TestCase("badWeight")][TestCase("missingCheck")][TestCase("missingPolicy")]
    public void IncompleteRankingContractsRemainDrafts(string defect)
    {
        var source=Source();switch(defect)
        {
            case "badWeight":source["ScoringContract"]!["RankingPolicy"]!["Weights"]!["usefulness"]=99;break;
            case "missingPolicy":source["ScoringContract"]!.AsObject().Remove("RankingPolicy");break;
            case "missingCheck":foreach(var a in source["Actions"]!.AsArray())foreach(var step in a!["Steps"]!.AsArray())if(step!["CapabilityKey"]!.GetValue<string>()=="ScoreIdeaQuality")step["AcceptanceCheckKeys"]!.AsArray().RemoveAt(step["AcceptanceCheckKeys"]!.AsArray().Count-1);break;
        }
        Assert.Throws<InvalidOperationException>(()=>FullWorkflowProductionContract.Validate(FullWorkflowSpecification.Parse(source.ToJsonString())));
    }
}
