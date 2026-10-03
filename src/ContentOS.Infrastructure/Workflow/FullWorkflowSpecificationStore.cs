using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ContentOS.Infrastructure.Workflow;

public sealed record FullSpecificationStep(Guid SourceId,string CapabilityKey,string Name,int Order,string Instructions,string ExpectedOutput,IReadOnlyList<string> AcceptanceCheckKeys);
public sealed record FullSpecificationAction(Guid SourceId,string CapabilityKey,string Name,int Order,string Instructions,string ExpectedOutput,IReadOnlyList<FullSpecificationStep> Steps);
public sealed record FullWorkflowSpecification(Guid SourceId,WorkflowDefinitionType WorkflowType,string Name,int SourceVersion,JsonElement Root,IReadOnlyList<FullSpecificationAction> Actions)
{
    public static FullWorkflowSpecification Parse(string json)
    {
        try { return ParseCore(json); }
        catch(Exception ex) when(ex is KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidOperationException("Workflow specification has missing or malformed required fields.",ex); }
    }
    private static FullWorkflowSpecification ParseCore(string json)
    {
        if(json.Length>1_000_000)throw new InvalidOperationException("Workflow specification exceeds the local data bound.");
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        RejectDuplicateProperties(root);
        var id=RequiredId(root,"Id");var name=RequiredText(root,"Name");
        if(!Enum.TryParse<WorkflowDefinitionType>(RequiredText(root,"WorkflowType"),false,out var type)||!Enum.IsDefined(type))throw new InvalidOperationException("Unsupported workflow type.");
        var version=root.GetProperty("Version").GetInt32();if(version<1)throw new InvalidOperationException("Specification version must be positive.");
        if(!root.TryGetProperty("ScoringContract",out var scoring)||scoring.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Full revised specification requires its scoring contract.");
        var actions=new List<FullSpecificationAction>();var ids=new HashSet<Guid>{id};
        foreach(var action in root.GetProperty("Actions").EnumerateArray())
        {
            var actionId=RequiredId(action,"Id");if(!ids.Add(actionId))throw new InvalidOperationException("Duplicate logical identifier.");
            if(action.TryGetProperty("IsEnabled",out var enabled)&&!enabled.GetBoolean())throw new InvalidOperationException("Required revised actions cannot be silently disabled.");
            var steps=new List<FullSpecificationStep>();
            foreach(var step in action.GetProperty("Steps").EnumerateArray())
            {
                var stepId=RequiredId(step,"Id");if(!ids.Add(stepId))throw new InvalidOperationException("Duplicate logical identifier.");
                if(step.TryGetProperty("IsEnabled",out var stepEnabled)&&!stepEnabled.GetBoolean())throw new InvalidOperationException("Required revised steps cannot be silently disabled.");
                var checks=step.GetProperty("AcceptanceCheckKeys").EnumerateArray().Select(x=>x.GetString()??"").ToArray();
                if(checks.Length==0||checks.Any(string.IsNullOrWhiteSpace)||checks.Distinct(StringComparer.Ordinal).Count()!=checks.Length)throw new InvalidOperationException("Every step requires distinct acceptance checks.");
                steps.Add(new(stepId,RequiredText(step,"CapabilityKey"),RequiredText(step,"Name"),step.GetProperty("Order").GetInt32(),OptionalText(step,"Instructions"),RequiredText(step,"ExpectedOutput"),checks));
            }
            ValidateOrder(steps.Select(x=>x.Order));
            actions.Add(new(actionId,RequiredText(action,"CapabilityKey"),RequiredText(action,"Name"),action.GetProperty("Order").GetInt32(),OptionalText(action,"Instructions"),OptionalText(action,"ExpectedOutput"),steps.OrderBy(s=>s.Order).ToArray()));
        }
        ValidateOrder(actions.Select(x=>x.Order));
        return new(id,type,name,version,root.Clone(),actions.OrderBy(a=>a.Order).ToArray());
    }
    private static Guid RequiredId(JsonElement value,string key)=>Guid.TryParse(RequiredText(value,key),out var id)&&id!=Guid.Empty?id:throw new InvalidOperationException($"Required valid identifier: {key}.");
    private static string RequiredText(JsonElement value,string key)=>value.TryGetProperty(key,out var text)&&text.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(text.GetString())?text.GetString()!:throw new InvalidOperationException($"Required text: {key}.");
    private static string OptionalText(JsonElement value,string key)=>value.TryGetProperty(key,out var text)&&text.ValueKind==JsonValueKind.String?text.GetString()??"":"";
    private static void RejectDuplicateProperties(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var property in value.EnumerateObject())
            { if(!names.Add(property.Name))throw new InvalidOperationException("Duplicate workflow JSON property: "+property.Name);RejectDuplicateProperties(property.Value); }
        }
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())RejectDuplicateProperties(item);
    }
    private static void ValidateOrder(IEnumerable<int> orders){var values=orders.ToArray();if(values.Length==0||values.Any(x=>x<1)||values.Distinct().Count()!=values.Length)throw new InvalidOperationException("Actions and steps require unique positive order values.");}
}

/// <summary>Preserves the complete specification as immutable draft evidence without schema changes or implicit activation.</summary>
public sealed class FullWorkflowSpecificationStore(ContentOsDbContext db,IFullWorkflowProductionAuthorization? authorization=null)
{
    public async Task<WorkflowDefinition> ImportDraftAsync(string json,Guid explicitlySelectedLocalFamily,string author,CancellationToken cancellationToken)
    {
        var spec=FullWorkflowSpecification.Parse(json);
        var family=await db.WorkflowDefinitionFamilies.SingleOrDefaultAsync(f=>f.Id==explicitlySelectedLocalFamily,cancellationToken)
            ??throw new InvalidOperationException("Explicit local workflow family binding is required.");
        if(family.WorkflowType!=spec.WorkflowType)throw new InvalidOperationException("Specification type differs from the selected local family.");
        var latest=await db.WorkflowDefinitions.Where(d=>d.WorkflowDefinitionFamilyId==family.Id).Select(d=>(int?)d.Version).MaxAsync(cancellationToken)??0;
        var definition=new WorkflowDefinition{Id=Guid.NewGuid(),WorkflowDefinitionFamilyId=family.Id,WorkflowType=spec.WorkflowType,Name=spec.Name,Version=latest+1,IsActive=false,Description="Revised full specification draft; source version and all policies retained in immutable specification evidence.",PublishedBy=""};
        db.WorkflowDefinitions.Add(definition);
        var mappings=new List<object>();
        foreach(var sourceAction in spec.Actions)
        {
            var action=new WorkflowActionDefinition{Id=Guid.NewGuid(),WorkflowDefinitionId=definition.Id,CapabilityKey=sourceAction.CapabilityKey,Name=sourceAction.Name,Order=sourceAction.Order,Instructions=sourceAction.Instructions,IsEnabled=true};
            db.WorkflowActionDefinitions.Add(action);mappings.Add(new{kind="action",sourceId=sourceAction.SourceId,localId=action.Id});
            foreach(var sourceStep in sourceAction.Steps)
            {
                var step=new WorkflowStepDefinition{Id=Guid.NewGuid(),WorkflowActionDefinitionId=action.Id,CapabilityKey=sourceStep.CapabilityKey,Name=sourceStep.Name,Order=sourceStep.Order,Instructions=sourceStep.Instructions,ExpectedOutput=sourceStep.ExpectedOutput,IsEnabled=true};
                db.WorkflowStepDefinitions.Add(step);mappings.Add(new{kind="step",sourceId=sourceStep.SourceId,localId=step.Id});
            }
        }
        db.WorkflowDefinitionMutations.Add(new WorkflowDefinitionMutation{Id=Guid.NewGuid(),WorkflowDefinitionId=definition.Id,MutationType="FullSpecificationDraft",Summary="Complete source JSON retained; local physical identifiers cloned; no activation.",NewValueJson=json,OldValueJson=JsonSerializer.Serialize(new{spec.SourceId,spec.SourceVersion,explicitLocalFamilyBinding=family.Id,mappings}),CreatedBy=author});
        await db.SaveChangesAsync(cancellationToken);return definition;
    }
    public async Task<string> GetSourceJsonAsync(Guid definitionId,CancellationToken cancellationToken)
        =>(await db.WorkflowDefinitionMutations.SingleOrDefaultAsync(m=>m.WorkflowDefinitionId==definitionId&&m.MutationType=="FullSpecificationDraft",cancellationToken))?.NewValueJson
            ??throw new InvalidOperationException("No full specification evidence exists for this definition.");
    public async Task<string?> TryGetSourceJsonAsync(Guid definitionId,CancellationToken cancellationToken)
        =>(await db.WorkflowDefinitionMutations.SingleOrDefaultAsync(m=>m.WorkflowDefinitionId==definitionId&&m.MutationType=="FullSpecificationDraft",cancellationToken))?.NewValueJson;

    public async Task ActivateAsync(Guid definitionId,IReadOnlyCollection<string> supportedProductionStepCapabilities,string author,CancellationToken cancellationToken)
    {
        var spec=FullWorkflowSpecification.Parse(await GetSourceJsonAsync(definitionId,cancellationToken));
        var missing=spec.Actions.SelectMany(a=>a.Steps).Select(s=>s.CapabilityKey).Where(k=>!supportedProductionStepCapabilities.Contains(k,StringComparer.Ordinal)).Distinct().ToArray();
        if(missing.Length>0)throw new InvalidOperationException("Activation blocked: unsupported production steps: "+string.Join(", ",missing));
        FullWorkflowProductionContract.Validate(spec,authorization);
        var definition=await db.WorkflowDefinitions.SingleAsync(d=>d.Id==definitionId,cancellationToken);
        var family=await db.WorkflowDefinitionFamilies.SingleAsync(f=>f.Id==definition.WorkflowDefinitionFamilyId,cancellationToken);
        var previous=await db.WorkflowDefinitions.Where(d=>d.WorkflowDefinitionFamilyId==family.Id&&d.IsActive).ToListAsync(cancellationToken);
        foreach(var old in previous)old.IsActive=false;
        definition.IsActive=true;definition.PublishedBy=author;definition.PublishedUtc=DateTime.UtcNow;family.ActiveWorkflowDefinitionId=definition.Id;family.UpdatedUtc=DateTime.UtcNow;
        db.WorkflowDefinitionMutations.Add(new(){Id=Guid.NewGuid(),WorkflowDefinitionId=definition.Id,MutationType="FullSpecificationActivation",Summary="Explicit activation after production capability preflight.",OldValueJson=JsonSerializer.Serialize(previous.Select(d=>d.Id)),NewValueJson=JsonSerializer.Serialize(new{definition.Id,definition.Version}),CreatedBy=author});
        await db.SaveChangesAsync(cancellationToken);
    }
}
