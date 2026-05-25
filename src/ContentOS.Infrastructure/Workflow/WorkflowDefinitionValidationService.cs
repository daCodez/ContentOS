using ContentOS.Application.DTOs;

namespace ContentOS.Infrastructure.Workflow;

public static class WorkflowDefinitionValidationService
{
    private static readonly string[] ArticleCapabilities =
    {
        "DraftArticle",
        "HumanizeArticle",
        "DeoptimizeArticle",
        "PreQaSeoValidation",
        "QaScoring"
    };

    private static readonly string[] IdeaCapabilities =
    {
        "ResearchPainPoints",
        "GenerateIdeas",
        "ScoreIdeas",
        "FilterDuplicates",
        "PersistIdeaRecords"
    };

    public static void NormalizeAndValidate(WorkflowDefinitionDto definition)
    {
        if (definition.Actions.Count == 0)
        {
            throw new InvalidOperationException("Workflow definition must contain at least one action.");
        }

        var orderedActions = definition.Actions.OrderBy(x => x.Order).ToList();
        for (var actionIndex = 0; actionIndex < orderedActions.Count; actionIndex++)
        {
            orderedActions[actionIndex].Order = actionIndex + 1;

            var orderedSteps = orderedActions[actionIndex].Steps.OrderBy(x => x.Order).ToList();
            for (var stepIndex = 0; stepIndex < orderedSteps.Count; stepIndex++)
            {
                orderedSteps[stepIndex].Order = stepIndex + 1;
            }

            if (orderedSteps.Select(x => x.Order).Distinct().Count() != orderedSteps.Count)
            {
                throw new InvalidOperationException($"Duplicate step order detected in action '{orderedActions[actionIndex].Name}'.");
            }

            orderedActions[actionIndex].Steps = orderedSteps;
        }

        if (orderedActions.Select(x => x.Order).Distinct().Count() != orderedActions.Count)
        {
            throw new InvalidOperationException("Duplicate action order detected.");
        }

        definition.Actions = orderedActions;

        var capabilityKeys = definition.Actions
            .Where(x => x.IsEnabled)
            .Select(x => x.CapabilityKey)
            .Concat(definition.Actions.SelectMany(x => x.Steps).Where(x => x.IsEnabled).Select(x => x.CapabilityKey))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredCapabilities = string.Equals(definition.WorkflowType, "Article", StringComparison.OrdinalIgnoreCase)
            ? ArticleCapabilities
            : IdeaCapabilities;

        foreach (var capability in requiredCapabilities)
        {
            if (!capabilityKeys.Contains(capability))
            {
                throw new InvalidOperationException($"Workflow must include required capability '{capability}'.");
            }
        }
    }
}
