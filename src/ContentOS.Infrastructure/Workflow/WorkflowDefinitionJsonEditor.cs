using System.Text.Json;
using System.Text.Json.Serialization;
using ContentOS.Application.DTOs;

namespace ContentOS.Infrastructure.Workflow;

/// <summary>Parses an editable definition without allowing JSON to switch the selected workflow identity.</summary>
public static class WorkflowDefinitionJsonEditor
{
    public static WorkflowDefinitionDto ParseAndValidate(string json, WorkflowDefinitionDto selected)
    {
        var definition = JsonSerializer.Deserialize<WorkflowDefinitionDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidOperationException("Enter a workflow definition JSON object.");
        if (definition.Id != selected.Id || definition.WorkflowDefinitionFamilyId != selected.WorkflowDefinitionFamilyId
            || definition.Version != selected.Version
            || !string.Equals(definition.WorkflowType, selected.WorkflowType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Keep the selected workflow's Id, family, type and version unchanged. Publishing creates the next version.");
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new InvalidOperationException("Workflow name is required.");
        if (definition.Actions is null || definition.Actions.Any(a => a is null || a.Steps is null || a.Steps.Any(s => s is null)))
            throw new InvalidOperationException("Actions and Steps must be arrays of objects.");
        WorkflowDefinitionValidationService.NormalizeAndValidate(definition);
        return definition;
    }
}
