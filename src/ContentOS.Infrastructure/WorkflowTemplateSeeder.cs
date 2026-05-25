using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Agents;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public static class WorkflowTemplateSeeder
{
    public static async Task SeedAsync(ContentOsDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var templateName = "LongFormBlogArticle";
        var now = DateTime.UtcNow;

        await SeedWorkflowFamiliesAsync(dbContext, now, cancellationToken);

        var existingTemplate = await dbContext.WorkflowTemplates
            .FirstOrDefaultAsync(x => x.Name == templateName, cancellationToken);

        if (existingTemplate is null)
        {
            existingTemplate = new WorkflowTemplate
            {
                Id = Guid.NewGuid(),
                Name = templateName,
                Description = "Standard workflow for creating a long-form SEO blog article from an approved topic.",
                IsActive = true,
                CreatedUtc = now,
                LastUpdatedUtc = now
            };

            dbContext.WorkflowTemplates.Add(existingTemplate);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existingTemplate.Description = "Standard workflow for creating a long-form SEO blog article from an approved topic.";
            existingTemplate.IsActive = true;
            existingTemplate.LastUpdatedUtc = now;
        }

        var existingTasks = await dbContext.WorkflowTaskTemplates
            .Where(x => x.WorkflowTemplateId == existingTemplate.Id)
            .ToListAsync(cancellationToken);

        var existingActions = await dbContext.WorkflowActionDefinitions
            .Where(x => x.WorkflowDefinitionId == existingTemplate.Id)
            .ToListAsync(cancellationToken);

        var existingSteps = await dbContext.WorkflowStepDefinitions
            .Where(x => existingActions.Select(a => a.Id).Contains(x.WorkflowActionDefinitionId))
            .ToListAsync(cancellationToken);

        var desiredTasks = new[]
        {
            CreateTask(existingTemplate.Id, 1, "Research Topic and Intent", "Research", AgentStack.ResearchAgent),
            CreateTask(existingTemplate.Id, 2, "Build Keyword Strategy", "SEO", AgentStack.KeywordAgent),
            CreateTask(existingTemplate.Id, 3, "Optimize SEO Package", "Optimization", AgentStack.SeoOptimizationLoopAgent),
            CreateTask(existingTemplate.Id, 4, "Create Structured Outline", "Planning", AgentStack.ContentPlannerAgent),
            CreateTask(existingTemplate.Id, 5, "Write Rule-Compliant Draft", "Writing", AgentStack.WriterAgent),
            CreateTask(existingTemplate.Id, 6, "Apply SEO and Linking", "SEO", AgentStack.SeoOptimizerAgent),
            CreateTask(existingTemplate.Id, 7, "Add Monetization and CTA", "Monetization", AgentStack.MonetizationAgent),
            CreateTask(existingTemplate.Id, 8, "Run Strict QA Scoring", "QA", AgentStack.QaScoringAgent),
            CreateTask(existingTemplate.Id, 9, "Humanize Final Draft", "Humanizer", AgentStack.HumanizerAgent)
        };

        foreach (var desired in desiredTasks)
        {
            var existing = existingTasks.FirstOrDefault(x => x.Name == desired.Name);
            if (existing is null)
            {
                dbContext.WorkflowTaskTemplates.Add(desired);
                continue;
            }

            existing.StageName = desired.StageName;
            existing.DisplayOrder = desired.DisplayOrder;
            existing.AssignedAgent = desired.AssignedAgent;
            existing.InstructionsTemplate = desired.InstructionsTemplate;
            existing.RequiredInputsJson = desired.RequiredInputsJson;
            existing.ExpectedOutputsJson = desired.ExpectedOutputsJson;
            existing.AutoStartWhenPreviousComplete = desired.AutoStartWhenPreviousComplete;
            existing.IsApprovalRequired = desired.IsApprovalRequired;
            existing.IsActive = true;
            existing.UpdatedUtc = now;
        }

        var desiredNames = desiredTasks.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingTasks.Where(x => !desiredNames.Contains(x.Name)))
        {
            existing.IsActive = false;
            existing.UpdatedUtc = now;
        }

        await SeedDefinitionActionsAsync(dbContext, existingTemplate, existingActions, existingSteps, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDefinitionActionsAsync(
        ContentOsDbContext dbContext,
        WorkflowTemplate template,
        List<WorkflowActionDefinition> existingActions,
        List<WorkflowStepDefinition> existingSteps,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var desiredActions = new[]
        {
            new
            {
                Name = "Research Topic and Intent",
                Description = "Research the topic, audience, and search landscape before writing.",
                AssignedAgent = AgentStack.ResearchAgent,
                Order = 1,
                Instructions = "Gather search intent, audience questions, and market context.",
                Steps = new[]
                {
                    new { Name = "Search Top Ranking Articles", Purpose = "Understand current SERP leaders.", Order = 1, Instructions = "Search the top 10 ranking pages for the primary keyword and capture main patterns.", ExpectedOutput = "Top ranking pages and summary" },
                    new { Name = "Extract Common Keywords", Purpose = "Identify recurring keyword patterns.", Order = 2, Instructions = "Extract recurring keywords and related phrases from leading pages.", ExpectedOutput = "Primary and secondary keyword list" },
                    new { Name = "Summarize Themes", Purpose = "Identify repeated themes and framing.", Order = 3, Instructions = "Summarize repeated angles, structures, and narrative patterns.", ExpectedOutput = "Theme summary" },
                    new { Name = "Analyze Audience Pain Points", Purpose = "Understand audience frustrations and questions.", Order = 4, Instructions = "Identify audience pain points, desired outcomes, and language patterns.", ExpectedOutput = "Pain points summary" }
                }
            },
            new
            {
                Name = "Build Keyword Strategy",
                Description = "Translate research into a usable SEO and topic coverage strategy.",
                AssignedAgent = AgentStack.KeywordAgent,
                Order = 2,
                Instructions = "Define the keyword plan for the article.",
                Steps = new[]
                {
                    new { Name = "Map Primary Keyword", Purpose = "Anchor the article around one clear primary query.", Order = 1, Instructions = "Select the main target keyword and confirm intent fit.", ExpectedOutput = "Primary keyword" },
                    new { Name = "Group Supporting Terms", Purpose = "Build semantic support around the primary keyword.", Order = 2, Instructions = "Group secondary keywords and related entities by intent.", ExpectedOutput = "Supporting term clusters" }
                }
            },
            new
            {
                Name = "Create Structured Outline",
                Description = "Create a reader-focused article structure.",
                AssignedAgent = AgentStack.ContentPlannerAgent,
                Order = 3,
                Instructions = "Produce the article structure and content flow.",
                Steps = new[]
                {
                    new { Name = "Draft Outline", Purpose = "Create a clear article structure.", Order = 1, Instructions = "Draft the H1, section hierarchy, and CTA path.", ExpectedOutput = "Outline" }
                }
            },
            new
            {
                Name = "Write Draft",
                Description = "Produce the first full article draft.",
                AssignedAgent = AgentStack.WriterAgent,
                Order = 4,
                Instructions = "Write the article draft using the approved plan.",
                Steps = new[]
                {
                    new { Name = "Write Rule-Compliant Draft", Purpose = "Generate the full article draft.", Order = 1, Instructions = "Write the article with the correct structure, tone, and factual framing.", ExpectedOutput = "Article draft" }
                }
            },
            new
            {
                Name = "QA",
                Description = "Evaluate readiness, accuracy, and coverage.",
                AssignedAgent = AgentStack.QaScoringAgent,
                Order = 5,
                Instructions = "Run quality assurance against the draft.",
                Steps = new[]
                {
                    new { Name = "Run Strict QA Scoring", Purpose = "Score the article and identify issues.", Order = 1, Instructions = "Evaluate clarity, completeness, risk, and publish readiness.", ExpectedOutput = "QA score and findings" }
                }
            },
            new
            {
                Name = "Humanize",
                Description = "Polish the final copy so it reads naturally.",
                AssignedAgent = AgentStack.HumanizerAgent,
                Order = 6,
                Instructions = "Improve readability and natural tone before final review.",
                Steps = new[]
                {
                    new { Name = "Humanize Final Draft", Purpose = "Reduce robotic phrasing and improve natural flow.", Order = 1, Instructions = "Rewrite awkward phrasing while preserving facts and structure.", ExpectedOutput = "Humanized article" }
                }
            }
        };

        foreach (var desired in desiredActions)
        {
            var action = existingActions.FirstOrDefault(x => x.Name == desired.Name);
            if (action is null)
            {
                action = new WorkflowActionDefinition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = template.Id,
                    Name = desired.Name,
                    Description = desired.Description,
                    AssignedAgent = desired.AssignedAgent,
                    Order = desired.Order,
                    Instructions = desired.Instructions,
                    IsEnabled = true,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                dbContext.WorkflowActionDefinitions.Add(action);
                existingActions.Add(action);
            }
            else
            {
                action.Description = desired.Description;
                action.AssignedAgent = desired.AssignedAgent;
                action.Order = desired.Order;
                action.Instructions = desired.Instructions;
                action.IsEnabled = true;
                action.UpdatedUtc = now;
            }

            foreach (var desiredStep in desired.Steps)
            {
                var step = existingSteps.FirstOrDefault(x => x.WorkflowActionDefinitionId == action.Id && x.Name == desiredStep.Name);
                if (step is null)
                {
                    step = new WorkflowStepDefinition
                    {
                        Id = Guid.NewGuid(),
                        WorkflowActionDefinitionId = action.Id,
                        Name = desiredStep.Name,
                        Purpose = desiredStep.Purpose,
                        Order = desiredStep.Order,
                        Instructions = desiredStep.Instructions,
                        ExpectedOutput = desiredStep.ExpectedOutput,
                        IsEnabled = true,
                        CreatedUtc = now,
                        UpdatedUtc = now
                    };
                    dbContext.WorkflowStepDefinitions.Add(step);
                    existingSteps.Add(step);
                }
                else
                {
                    step.Purpose = desiredStep.Purpose;
                    step.Order = desiredStep.Order;
                    step.Instructions = desiredStep.Instructions;
                    step.ExpectedOutput = desiredStep.ExpectedOutput;
                    step.IsEnabled = true;
                    step.UpdatedUtc = now;
                }
            }
        }

        await Task.CompletedTask;
    }

    private static async Task SeedWorkflowFamiliesAsync(ContentOsDbContext dbContext, DateTime now, CancellationToken cancellationToken)
    {
        var ideaFamily = await EnsureWorkflowFamilyAsync(
            dbContext,
            "Idea Pipeline",
            WorkflowDefinitionType.Idea,
            "Generates, scores, filters, and persists structured ideas before approval.",
            BuildIdeaDefinition,
            now,
            cancellationToken);

        await EnsureWorkflowFamilyAsync(
            dbContext,
            "Article Pipeline",
            WorkflowDefinitionType.Article,
            "Consumes an approved idea snapshot and executes deterministic article production.",
            BuildArticleDefinition,
            now,
            cancellationToken);
    }

    private static async Task<WorkflowDefinitionFamily> EnsureWorkflowFamilyAsync(
        ContentOsDbContext dbContext,
        string name,
        WorkflowDefinitionType workflowType,
        string description,
        Func<WorkflowDefinitionFamily, DateTime, WorkflowDefinitionSeedModel> buildDefinition,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var family = await dbContext.WorkflowDefinitionFamilies.FirstOrDefaultAsync(x => x.Name == name && x.WorkflowType == workflowType, cancellationToken);
        if (family is null)
        {
            family = new WorkflowDefinitionFamily
            {
                Id = Guid.NewGuid(),
                Name = name,
                WorkflowType = workflowType,
                Description = description,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            dbContext.WorkflowDefinitionFamilies.Add(family);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var activeDefinition = await dbContext.WorkflowDefinitions.FirstOrDefaultAsync(x => x.WorkflowDefinitionFamilyId == family.Id && x.IsActive, cancellationToken);
        if (activeDefinition is not null)
        {
            return family;
        }

        var seed = buildDefinition(family, now);
        var definition = new WorkflowDefinition
        {
            Id = seed.DefinitionId,
            WorkflowDefinitionFamilyId = family.Id,
            Version = 1,
            Name = family.Name,
            Description = family.Description,
            WorkflowType = family.WorkflowType,
            IsActive = true,
            CreatedUtc = now,
            PublishedUtc = now,
            PublishedBy = "Seeder"
        };

        dbContext.WorkflowDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        family.ActiveWorkflowDefinitionId = definition.Id;
        family.UpdatedUtc = now;
        dbContext.WorkflowDefinitionFamilies.Update(family);

        foreach (var actionSeed in seed.Actions)
        {
            var action = new WorkflowActionDefinition
            {
                Id = actionSeed.Id,
                WorkflowDefinitionId = definition.Id,
                CapabilityKey = actionSeed.CapabilityKey,
                Name = actionSeed.Name,
                Description = actionSeed.Description,
                AssignedAgent = actionSeed.AssignedAgent,
                Order = actionSeed.Order,
                Instructions = actionSeed.Instructions,
                IsEnabled = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };

            dbContext.WorkflowActionDefinitions.Add(action);

            foreach (var stepSeed in actionSeed.Steps)
            {
                dbContext.WorkflowStepDefinitions.Add(new WorkflowStepDefinition
                {
                    Id = stepSeed.Id,
                    WorkflowActionDefinitionId = action.Id,
                    CapabilityKey = stepSeed.CapabilityKey,
                    Name = stepSeed.Name,
                    Purpose = stepSeed.Purpose,
                    Order = stepSeed.Order,
                    Instructions = stepSeed.Instructions,
                    ExpectedOutput = stepSeed.ExpectedOutput,
                    IsEnabled = true,
                    CreatedUtc = now,
                    UpdatedUtc = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return family;
    }

    private static WorkflowDefinitionSeedModel BuildIdeaDefinition(WorkflowDefinitionFamily family, DateTime now)
    {
        return new WorkflowDefinitionSeedModel
        {
            DefinitionId = Guid.NewGuid(),
            Actions =
            [
                new WorkflowActionSeedModel(
                    Guid.NewGuid(), "ResearchPainPoints", "Research Pain Points", "Research audience pain and intent.", AgentStack.ResearchAgent, 1, "Identify urgent beginner problems and real search intent.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "ResearchPainPoints", "Research Pain Points", "Find urgent budgeting pain points.", 1, "Extract recurring financial pain points and audience language.", "Pain point findings")]),
                new WorkflowActionSeedModel(
                    Guid.NewGuid(), "GenerateIdeas", "Generate Ideas", "Generate structured topic ideas.", AgentStack.ContentStrategyAgent, 2, "Generate 20 to 30 candidate ideas with clear audience and pain.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "GenerateIdeas", "Generate Structured Ideas", "Produce structured idea candidates.", 1, "Generate structured ideas including title, problem, audience, trigger, and angle.", "Structured ideas")]),
                new WorkflowActionSeedModel(
                    Guid.NewGuid(), "ScoreIdeas", "Score Ideas", "Score ideas for quality and usefulness.", AgentStack.ContentStrategyAgent, 3, "Score ideas on pain, clarity, uniqueness, usefulness, and monetization fit.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "ScoreIdeas", "Score Ideas", "Score each idea consistently.", 1, "Assign monetization, SEO, difficulty, and priority scores.", "Idea scores")]),
                new WorkflowActionSeedModel(
                    Guid.NewGuid(), "FilterDuplicates", "Filter Duplicates", "Remove duplicates and weak ideas.", AgentStack.EditorialAgent, 4, "Cluster duplicates and reject generic or keyword-shaped ideas.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "FilterDuplicates", "Filter Duplicates", "Keep the best idea set.", 1, "Remove vague, duplicate, and weak ideas. Keep top candidates only.", "Filtered idea list")]),
                new WorkflowActionSeedModel(
                    Guid.NewGuid(), "PersistIdeaRecords", "Persist Idea Records", "Persist structured ideas for approval.", AgentStack.EditorialAgent, 5, "Persist structured ideas into IdeaRecord rows for Mission Control approval.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "PersistIdeaRecords", "Persist Idea Records", "Save idea records.", 1, "Write structured IdeaRecord rows for downstream approval and article handoff.", "Persisted idea records")])
            ]
        };
    }

    private static WorkflowDefinitionSeedModel BuildArticleDefinition(WorkflowDefinitionFamily family, DateTime now)
    {
        return new WorkflowDefinitionSeedModel
        {
            DefinitionId = Guid.NewGuid(),
            Actions =
            [
                new WorkflowActionSeedModel(Guid.NewGuid(), "DraftArticle", "Draft", "Create the article draft from the approved idea.", AgentStack.WriterAgent, 1, "Write from the approved idea snapshot without redefining topic direction.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "DraftArticle", "Write Draft", "Draft the article.", 1, "Write a full draft using ideaId, ideaTitle, readerProblem, audience, and angle.", "Article draft")]),
                new WorkflowActionSeedModel(Guid.NewGuid(), "HumanizeArticle", "Humanize", "Improve readability and naturalness.", AgentStack.HumanizerAgent, 2, "Humanize the article without changing topic direction.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "HumanizeArticle", "Humanize Draft", "Humanize the draft.", 1, "Reduce robotic phrasing and improve flow.", "Humanized article")]),
                new WorkflowActionSeedModel(Guid.NewGuid(), "DeoptimizeArticle", "De-optimization", "Remove keyword repetition and SEO-shaped phrasing.", AgentStack.HumanizerAgent, 3, "Remove repetitive exact-match phrasing before QA.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "DeoptimizeArticle", "Remove Keyword Repetition", "De-optimize the article.", 1, "Normalize repetitive keyword phrasing while preserving meaning.", "De-optimized article")]),
                new WorkflowActionSeedModel(Guid.NewGuid(), "PreQaSeoValidation", "Pre-QA SEO Validation", "Validate the article before QA scoring.", AgentStack.QaScoringAgent, 4, "Fail fast if the article still violates SEO phrasing rules.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "PreQaSeoValidation", "Validate SEO Phrasing", "Validate phrase quality.", 1, "Validate heading and body exact-match thresholds before QA.", "SEO validation result")]),
                new WorkflowActionSeedModel(Guid.NewGuid(), "QaScoring", "QA", "Run final QA scoring.", AgentStack.QaScoringAgent, 5, "Score the cleaned article only after pre-QA validation passes.",
                    [new WorkflowStepSeedModel(Guid.NewGuid(), "QaScoring", "Run QA Scoring", "Score the article.", 1, "Evaluate clarity, completeness, and publish readiness.", "QA score")])
            ]
        };
    }

    private static WorkflowTaskTemplate CreateTask(Guid workflowTemplateId, int order, string name, string stage, string assignedAgent)
    {
        return new WorkflowTaskTemplate
        {
            Id = Guid.NewGuid(),
            WorkflowTemplateId = workflowTemplateId,
            Name = name,
            StageName = stage,
            DisplayOrder = order,
            AssignedAgent = assignedAgent,
            InstructionsTemplate = name,
            RequiredInputsJson = "[]",
            ExpectedOutputsJson = "[]",
            AutoStartWhenPreviousComplete = true,
            IsApprovalRequired = false,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private sealed class WorkflowDefinitionSeedModel
    {
        public Guid DefinitionId { get; init; }
        public List<WorkflowActionSeedModel> Actions { get; init; } = new();
    }

    private sealed record WorkflowActionSeedModel(Guid Id, string CapabilityKey, string Name, string Description, string AssignedAgent, int Order, string Instructions, List<WorkflowStepSeedModel> Steps);
    private sealed record WorkflowStepSeedModel(Guid Id, string CapabilityKey, string Name, string Purpose, int Order, string Instructions, string ExpectedOutput);
}
