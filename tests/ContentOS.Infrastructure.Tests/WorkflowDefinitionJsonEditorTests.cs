using System.Text.Json;
using System.Text.Json.Nodes;
using ContentOS.Application.DTOs;
using ContentOS.Infrastructure.Workflow;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;
public class WorkflowDefinitionJsonEditorTests
{
    private static WorkflowDefinitionDto Definition() => new()
    {
        Id=Guid.NewGuid(), WorkflowDefinitionFamilyId=Guid.NewGuid(), Version=1,
        Name="Article Pipeline", WorkflowType="Article",
        Actions=new[]{"DraftArticle","HumanizeArticle","DeoptimizeArticle","PreQaSeoValidation","QaScoring"}
            .Select((capability,index)=>new WorkflowActionDefinitionDto { Id=Guid.NewGuid(),Name=capability,CapabilityKey=capability,Order=index+1,IsEnabled=true }).ToList()
    };
    [Test] public void ValidEditsAreReturnedWithoutMutatingSelectedDefinition()
    {
        var selected=Definition(); var json=JsonSerializer.Serialize(selected).Replace("Article Pipeline","My article pipeline");
        var parsed=WorkflowDefinitionJsonEditor.ParseAndValidate(json,selected);
        Assert.That(parsed.Name,Is.EqualTo("My article pipeline")); Assert.That(selected.Name,Is.EqualTo("Article Pipeline"));
    }
    [Test] public void InvalidSyntaxIsRejected() => Assert.Throws<JsonException>(()=>WorkflowDefinitionJsonEditor.ParseAndValidate("{ broken",Definition()));
    [Test] public void ChangingSelectedIdentityIsRejected()
    {
        var selected=Definition();var different=Definition();
        Assert.Throws<InvalidOperationException>(()=>WorkflowDefinitionJsonEditor.ParseAndValidate(JsonSerializer.Serialize(different),selected));
    }
    [Test] public void MissingRequiredCapabilityIsRejected()
    {
        var selected=Definition();var edited=JsonSerializer.Deserialize<WorkflowDefinitionDto>(JsonSerializer.Serialize(selected))!;
        edited.Actions.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(()=>WorkflowDefinitionJsonEditor.ParseAndValidate(JsonSerializer.Serialize(edited),selected));
    }
    [Test] public void NullActionsAreRejected()
    {
        var selected=Definition();var edited=JsonSerializer.Deserialize<WorkflowDefinitionDto>(JsonSerializer.Serialize(selected))!;edited.Actions=null!;
        Assert.Throws<InvalidOperationException>(()=>WorkflowDefinitionJsonEditor.ParseAndValidate(JsonSerializer.Serialize(edited),selected));
    }
    [TestCase("root")]
    [TestCase("action")]
    [TestCase("step")]
    public void UnsupportedFieldsAreRejectedInsteadOfSilentlyDiscarded(string level)
    {
        var selected=Definition();var node=JsonNode.Parse(JsonSerializer.Serialize(selected))!;
        if(level=="root") node["RequiresHumanApproval"]=true;
        else if(level=="action") node["Actions"]![0]!["ExpectedOutput"]="Requested output";
        else node["Actions"]![0]!["Steps"]!.AsArray().Add(new JsonObject { ["UnsupportedPolicy"]=true });
        Assert.Throws<JsonException>(()=>WorkflowDefinitionJsonEditor.ParseAndValidate(node.ToJsonString(),selected));
    }
}
