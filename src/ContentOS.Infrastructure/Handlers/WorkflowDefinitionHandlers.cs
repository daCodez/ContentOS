using System.Text.Json;
using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Application.Abstractions;
using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Handlers;

public class GetWorkflowDefinitionsQueryHandler : IRequestHandler<GetWorkflowDefinitionsQuery, IEnumerable<WorkflowDefinitionDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowDefinitionsQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<WorkflowDefinitionDto>> Handle(GetWorkflowDefinitionsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkflowDefinitions
            .AsNoTracking()
            .OrderBy(x => x.WorkflowType)
            .ThenBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .Select(x => new WorkflowDefinitionDto
            {
                Id = x.Id,
                WorkflowDefinitionFamilyId = x.WorkflowDefinitionFamilyId,
                WorkflowType = x.WorkflowType.ToString(),
                Name = x.Name,
                Description = x.Description,
                Version = x.Version,
                IsActive = x.IsActive,
                LastUpdatedUtc = x.PublishedUtc
            })
            .ToListAsync(cancellationToken);
    }
}

public class GetWorkflowDefinitionByIdQueryHandler : IRequestHandler<GetWorkflowDefinitionByIdQuery, WorkflowDefinitionDto?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowDefinitionByIdQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowDefinitionDto?> Handle(GetWorkflowDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var definition = await _dbContext.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.WorkflowDefinitionId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        var actions = await _dbContext.WorkflowActionDefinitions
            .AsNoTracking()
            .Where(x => x.WorkflowDefinitionId == request.WorkflowDefinitionId)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        var actionIds = actions.Select(x => x.Id).ToList();
        var steps = await _dbContext.WorkflowStepDefinitions
            .AsNoTracking()
            .Where(x => actionIds.Contains(x.WorkflowActionDefinitionId))
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        return new WorkflowDefinitionDto
        {
            Id = definition.Id,
            WorkflowDefinitionFamilyId = definition.WorkflowDefinitionFamilyId,
            WorkflowType = definition.WorkflowType.ToString(),
            Name = definition.Name,
            Description = definition.Description,
            Version = definition.Version,
            IsActive = definition.IsActive,
            LastUpdatedUtc = definition.PublishedUtc,
            Actions = actions.Select(action => new WorkflowActionDefinitionDto
            {
                Id = action.Id,
                WorkflowDefinitionId = action.WorkflowDefinitionId,
                CapabilityKey = action.CapabilityKey,
                Name = action.Name,
                Description = action.Description,
                AssignedAgent = action.AssignedAgent,
                Order = action.Order,
                Instructions = action.Instructions,
                IsEnabled = action.IsEnabled,
                Steps = steps.Where(step => step.WorkflowActionDefinitionId == action.Id)
                    .OrderBy(step => step.Order)
                    .Select(step => new WorkflowStepDefinitionDto
                    {
                        Id = step.Id,
                        WorkflowActionDefinitionId = step.WorkflowActionDefinitionId,
                        CapabilityKey = step.CapabilityKey,
                        Name = step.Name,
                        Purpose = step.Purpose,
                        Order = step.Order,
                        Instructions = step.Instructions,
                        ExpectedOutput = step.ExpectedOutput,
                        IsEnabled = step.IsEnabled
                    })
                    .ToList()
            }).ToList()
        };
    }
}

public class ProposeWorkflowDefinitionChangeCommandHandler : IRequestHandler<ProposeWorkflowDefinitionChangeCommand, WorkflowDefinitionChangeProposalDto>
{
    private readonly IMediator _mediator;

    public ProposeWorkflowDefinitionChangeCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<WorkflowDefinitionChangeProposalDto> Handle(ProposeWorkflowDefinitionChangeCommand request, CancellationToken cancellationToken)
    {
        var definition = await _mediator.Send(new GetWorkflowDefinitionByIdQuery(request.WorkflowDefinitionId), cancellationToken)
            ?? throw new InvalidOperationException("Workflow definition not found.");

        var normalized = request.UserRequest.Trim();
        var lower = normalized.ToLowerInvariant();
        var targetAction = InferTargetAction(definition, lower);
        var action = definition.Actions.FirstOrDefault(x => x.Name == targetAction) ?? definition.Actions.First();
        var stepName = InferStepName(normalized);
        var purpose = InferPurpose(normalized, stepName);
        var instructions = InferInstructions(normalized, stepName);
        var expectedOutput = InferExpectedOutput(stepName);
        var overlaps = action.Steps
            .Where(x => x.Name.Contains(stepName, StringComparison.OrdinalIgnoreCase)
                     || stepName.Contains(x.Name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var warnings = new List<string>();
        if (overlaps.Count > 0)
        {
            warnings.Add("This request overlaps with an existing step and may duplicate work.");
        }

        var insertPosition = action.Steps.Count + 1;
        if (lower.Contains("before") && action.Steps.Count > 0)
        {
            insertPosition = Math.Max(1, action.Steps.Count - 1);
        }

        return new WorkflowDefinitionChangeProposalDto
        {
            UserRequest = request.UserRequest,
            InterpretedIntent = $"Add or refine a workflow step within {action.Name}.",
            TargetAction = action.Name,
            ProposedChangeType = "AddStep",
            InsertPosition = insertPosition,
            ProposedStep = new WorkflowStepDefinitionDto
            {
                Name = stepName,
                Purpose = purpose,
                Order = insertPosition,
                Instructions = instructions,
                ExpectedOutput = expectedOutput,
                IsEnabled = true
            },
            Warnings = warnings,
            OverlapsExistingSteps = overlaps,
            PreviewSummary = $"Add '{stepName}' to {action.Name} at position {insertPosition}."
        };
    }

    private static string InferTargetAction(WorkflowDefinitionDto definition, string lower)
    {
        if (lower.Contains("reddit") || lower.Contains("competitor") || lower.Contains("pain point") || lower.Contains("research"))
        {
            return definition.Actions.FirstOrDefault(x => x.Name.Contains("Research", StringComparison.OrdinalIgnoreCase))?.Name
                ?? definition.Actions.First().Name;
        }

        if (lower.Contains("qa") || lower.Contains("coverage"))
        {
            return definition.Actions.FirstOrDefault(x => x.Name.Contains("QA", StringComparison.OrdinalIgnoreCase))?.Name
                ?? definition.Actions.First().Name;
        }

        if (lower.Contains("faq") || lower.Contains("write") || lower.Contains("draft"))
        {
            return definition.Actions.FirstOrDefault(x => x.Name.Contains("Write", StringComparison.OrdinalIgnoreCase))?.Name
                ?? definition.Actions.First().Name;
        }

        return definition.Actions.First().Name;
    }

    private static string InferStepName(string request)
    {
        var cleaned = request.Trim().TrimEnd('.');
        if (cleaned.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[4..];
        }

        cleaned = cleaned.Replace("a step to", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("step", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "New Workflow Step";
        }

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string InferPurpose(string request, string stepName)
    {
        if (request.Contains("competitor", StringComparison.OrdinalIgnoreCase))
        {
            return "Identify gaps and differentiation opportunities from competing content.";
        }

        if (request.Contains("reddit", StringComparison.OrdinalIgnoreCase))
        {
            return "Capture real audience pain points and language from community discussions.";
        }

        if (request.Contains("faq", StringComparison.OrdinalIgnoreCase))
        {
            return "Generate useful question and answer coverage that strengthens the article.";
        }

        return $"Improve the workflow by adding {stepName}.";
    }

    private static string InferInstructions(string request, string stepName)
    {
        if (request.Contains("competitor", StringComparison.OrdinalIgnoreCase))
        {
            return "Search top 5 ranking pages for the primary keyword. Extract H1 and H2 headings. Identify repeated themes, missing angles, and summarize opportunities for a better article.";
        }

        if (request.Contains("reddit", StringComparison.OrdinalIgnoreCase))
        {
            return "Search relevant Reddit threads. Extract repeated frustrations, phrasing, questions, and examples that reveal audience pain points.";
        }

        if (request.Contains("coverage", StringComparison.OrdinalIgnoreCase) || request.Contains("qa", StringComparison.OrdinalIgnoreCase))
        {
            return "Check keyword coverage against the outline and final draft. Flag missing primary, secondary, and semantic term coverage.";
        }

        return $"Execute the {stepName} step and return structured findings for downstream actions.";
    }

    private static string InferExpectedOutput(string stepName)
    {
        return $"Structured output for {stepName}";
    }
}

public class ApplyWorkflowDefinitionChangeCommandHandler : IRequestHandler<ApplyWorkflowDefinitionChangeCommand, WorkflowDefinitionDto>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly IMediator _mediator;

    public ApplyWorkflowDefinitionChangeCommandHandler(ContentOsDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<WorkflowDefinitionDto> Handle(ApplyWorkflowDefinitionChangeCommand request, CancellationToken cancellationToken)
    {
        var definition = await _dbContext.WorkflowTemplates.FirstOrDefaultAsync(x => x.Id == request.WorkflowDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow definition not found.");

        var action = await _dbContext.WorkflowActionDefinitions
            .Where(x => x.WorkflowDefinitionId == request.WorkflowDefinitionId && x.Name == request.Proposal.TargetAction)
            .OrderBy(x => x.Order)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Target action not found.");

        var existingSteps = await _dbContext.WorkflowStepDefinitions
            .Where(x => x.WorkflowActionDefinitionId == action.Id)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingSteps.Where(x => x.Order >= request.Proposal.InsertPosition))
        {
            existing.Order += 1;
            existing.UpdatedUtc = DateTime.UtcNow;
        }

        var proposedStep = request.Proposal.ProposedStep ?? throw new InvalidOperationException("No proposed step found.");
        var step = new WorkflowStepDefinition
        {
            Id = Guid.NewGuid(),
            WorkflowActionDefinitionId = action.Id,
            Name = proposedStep.Name,
            Purpose = proposedStep.Purpose,
            Order = request.Proposal.InsertPosition,
            Instructions = proposedStep.Instructions,
            ExpectedOutput = proposedStep.ExpectedOutput,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.WorkflowStepDefinitions.Add(step);

        definition.Version += 1;
        definition.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.WorkflowDefinitionMutations.Add(new WorkflowDefinitionMutation
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definition.Id,
            MutationType = request.Proposal.ProposedChangeType,
            Summary = request.Proposal.PreviewSummary,
            OldValueJson = JsonSerializer.Serialize(new { Action = action.Name, Steps = existingSteps.Select(x => new { x.Name, x.Order }) }),
            NewValueJson = JsonSerializer.Serialize(request.Proposal),
            CreatedBy = request.CreatedBy,
            CreatedUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await _mediator.Send(new GetWorkflowDefinitionByIdQuery(definition.Id), cancellationToken)
            ?? throw new InvalidOperationException("Saved workflow definition could not be reloaded.");
    }
}

public class GetWorkflowDefinitionHistoryQueryHandler : IRequestHandler<GetWorkflowDefinitionHistoryQuery, IEnumerable<WorkflowDefinitionMutationHistoryDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowDefinitionHistoryQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<WorkflowDefinitionMutationHistoryDto>> Handle(GetWorkflowDefinitionHistoryQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkflowDefinitionMutations
            .AsNoTracking()
            .Where(x => x.WorkflowDefinitionId == request.WorkflowDefinitionId)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new WorkflowDefinitionMutationHistoryDto
            {
                Id = x.Id,
                WorkflowDefinitionId = x.WorkflowDefinitionId,
                MutationType = x.MutationType,
                Summary = x.Summary,
                CreatedBy = x.CreatedBy,
                CreatedUtc = x.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }
}

public class GetWorkflowJobVersionInfoQueryHandler : IRequestHandler<GetWorkflowJobVersionInfoQuery, WorkflowJobVersionInfoDto?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowJobVersionInfoQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowJobVersionInfoDto?> Handle(GetWorkflowJobVersionInfoQuery request, CancellationToken cancellationToken)
    {
        return await (from job in _dbContext.ContentWorkflowJobs.AsNoTracking()
                      join definition in _dbContext.WorkflowTemplates.AsNoTracking() on job.WorkflowTemplateId equals definition.Id
                      where job.Id == request.WorkflowJobId
                      select new WorkflowJobVersionInfoDto
                      {
                          WorkflowJobId = job.Id,
                          WorkflowDefinitionId = definition.Id,
                          WorkflowDefinitionName = definition.Name,
                          WorkflowDefinitionVersion = job.WorkflowTemplateVersion
                      }).FirstOrDefaultAsync(cancellationToken);
    }
}

public class SaveWorkflowDefinitionCommandHandler : IRequestHandler<SaveWorkflowDefinitionCommand, WorkflowDefinitionDto>
{
    private readonly IMediator _mediator;

    public SaveWorkflowDefinitionCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<WorkflowDefinitionDto> Handle(SaveWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        WorkflowDefinitionValidationService.NormalizeAndValidate(request.Definition);
        return _mediator.Send(new GetWorkflowDefinitionByIdQuery(request.Definition.Id), cancellationToken)!;
    }
}

public class PublishWorkflowDefinitionVersionCommandHandler : IRequestHandler<PublishWorkflowDefinitionVersionCommand, WorkflowDefinitionDto>
{
    private readonly WorkflowPipelineOrchestrator _workflowPipelineOrchestrator;

    public PublishWorkflowDefinitionVersionCommandHandler(WorkflowPipelineOrchestrator workflowPipelineOrchestrator)
    {
        _workflowPipelineOrchestrator = workflowPipelineOrchestrator;
    }

    public Task<WorkflowDefinitionDto> Handle(PublishWorkflowDefinitionVersionCommand request, CancellationToken cancellationToken)
    {
        return _workflowPipelineOrchestrator.PublishAsync(request.Definition, request.PublishedBy, cancellationToken);
    }
}

public class GetWorkflowDefinitionFamiliesQueryHandler : IRequestHandler<GetWorkflowDefinitionFamiliesQuery, IEnumerable<WorkflowDefinitionFamilyDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowDefinitionFamiliesQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<WorkflowDefinitionFamilyDto>> Handle(GetWorkflowDefinitionFamiliesQuery request, CancellationToken cancellationToken)
    {
        var families = await _dbContext.WorkflowDefinitionFamilies.AsNoTracking().OrderBy(x => x.WorkflowType).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var definitions = await _dbContext.WorkflowDefinitions.AsNoTracking().OrderByDescending(x => x.Version).ToListAsync(cancellationToken);

        return families.Select(family => new WorkflowDefinitionFamilyDto
        {
            Id = family.Id,
            Name = family.Name,
            WorkflowType = family.WorkflowType.ToString(),
            Description = family.Description,
            ActiveWorkflowDefinitionId = family.ActiveWorkflowDefinitionId,
            Versions = definitions.Where(x => x.WorkflowDefinitionFamilyId == family.Id)
                .Select(x => new WorkflowDefinitionDto
                {
                    Id = x.Id,
                    WorkflowDefinitionFamilyId = x.WorkflowDefinitionFamilyId,
                    WorkflowType = x.WorkflowType.ToString(),
                    Name = x.Name,
                    Description = x.Description,
                    Version = x.Version,
                    IsActive = x.IsActive,
                    LastUpdatedUtc = x.PublishedUtc
                }).ToList()
        }).ToList();
    }
}

public class GetWorkflowDefinitionVersionsQueryHandler : IRequestHandler<GetWorkflowDefinitionVersionsQuery, IEnumerable<WorkflowDefinitionDto>>
{
    private readonly IMediator _mediator;

    public GetWorkflowDefinitionVersionsQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IEnumerable<WorkflowDefinitionDto>> Handle(GetWorkflowDefinitionVersionsQuery request, CancellationToken cancellationToken)
    {
        var all = await _mediator.Send(new GetWorkflowDefinitionsQuery(), cancellationToken);
        return all.Where(x => x.WorkflowDefinitionFamilyId == request.WorkflowDefinitionFamilyId).OrderByDescending(x => x.Version);
    }
}

public class GetIdeaQueueQueryHandler : IRequestHandler<GetIdeaQueueQuery, IEnumerable<IdeaRecordDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetIdeaQueueQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<IdeaRecordDto>> Handle(GetIdeaQueueQuery request, CancellationToken cancellationToken)
    {
        var articleRuns = await _dbContext.WorkflowDefinitionRuns.AsNoTracking().Where(x => x.WorkflowType == WorkflowDefinitionType.Article && x.IdeaRecordId != null).ToListAsync(cancellationToken);

        var ideas = await _dbContext.IdeaRecords
            .AsNoTracking()
            .OrderByDescending(x => x.PriorityScore)
            .ThenByDescending(x => x.CreatedUtc)
            .Select(x => new
            {
                x.Id,
                x.SiteId,
                x.SourceWorkflowRunId,
                x.IdeaTitle,
                x.ReaderProblem,
                x.AudienceType,
                x.SearchIntent,
                x.EmotionalTrigger,
                x.UniquenessAngle,
                x.MonetizationFit,
                x.SeoPotential,
                x.Difficulty,
                x.PriorityScore,
                x.IdeaSnapshotJson,
                Status = x.Status.ToString(),
                x.CreatedUtc,
                x.ApprovedUtc
            })
            .ToListAsync(cancellationToken);

        return ideas.Select(x => new IdeaRecordDto
            {
                Id = x.Id,
                SiteId = x.SiteId,
                SourceWorkflowRunId = x.SourceWorkflowRunId,
                ArticleWorkflowRunId = articleRuns.Where(run => run.IdeaRecordId == x.Id).OrderByDescending(run => run.StartedUtc).Select(run => (Guid?)run.Id).FirstOrDefault(),
                IdeaTitle = x.IdeaTitle,
                ReaderProblem = x.ReaderProblem,
                AudienceType = x.AudienceType,
                SearchIntent = x.SearchIntent,
                EmotionalTrigger = x.EmotionalTrigger,
                UniquenessAngle = x.UniquenessAngle,
                MonetizationFit = x.MonetizationFit,
                SeoPotential = x.SeoPotential,
                Difficulty = x.Difficulty,
                PriorityScore = x.PriorityScore,
                ReviewedRanking = ReadReviewedRanking(x.IdeaSnapshotJson),
                Status = x.Status,
                CreatedUtc = x.CreatedUtc,
                ApprovedUtc = x.ApprovedUtc
            }).OrderByDescending(x=>x.ReviewedRanking is not null).ThenByDescending(x=>x.PriorityScore).ThenByDescending(x=>x.CreatedUtc).ToList();
    }
    private static ContentOS.Application.Research.IdeaRankingResult? ReadReviewedRanking(string json)
    {
        try
        {
            using var snapshot=System.Text.Json.JsonDocument.Parse(json);
            return snapshot.RootElement.TryGetProperty("reviewedRanking",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.Object
                ?System.Text.Json.JsonSerializer.Deserialize<ContentOS.Application.Research.IdeaRankingResult>(value.GetRawText()):null;
        }
        catch(System.Text.Json.JsonException){return null;}
    }
}

public class ApproveIdeaRecordCommandHandler : IRequestHandler<ApproveIdeaRecordCommand, Guid>
{
    private readonly IWorkflowBootstrapService _workflowBootstrapService;

    public ApproveIdeaRecordCommandHandler(IWorkflowBootstrapService workflowBootstrapService)
    {
        _workflowBootstrapService = workflowBootstrapService;
    }

    public Task<Guid> Handle(ApproveIdeaRecordCommand request, CancellationToken cancellationToken)
    {
        return _workflowBootstrapService.ApproveIdeaRecordAndCreateArticleWorkflowAsync(request.IdeaRecordId, request.ApprovedBy, cancellationToken);
    }
}

public class RejectIdeaRecordCommandHandler : IRequestHandler<RejectIdeaRecordCommand>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly ILogger<RejectIdeaRecordCommandHandler> _logger;

    public RejectIdeaRecordCommandHandler(ContentOsDbContext dbContext, ILogger<RejectIdeaRecordCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(RejectIdeaRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await _dbContext.IdeaRecords.FindAsync(new object[] { request.IdeaRecordId }, cancellationToken);

        if (record is null)
            throw new InvalidOperationException("Idea record not found.");

        // Delete the IdeaRecord instead of soft-rejecting it
        _logger.LogInformation(
            "Deleting rejected idea record: Id={Id}, Title='{Title}', RejectedBy='{RejectedBy}'",
            record.Id, record.IdeaTitle, request.RejectedBy);

        // Also delete any orphaned ContentIdea with the same title (from legacy pipeline)
        var matchingContentIdea = await _dbContext.ContentIdeas
            .FirstOrDefaultAsync(x => x.Title == record.IdeaTitle, cancellationToken);
        if (matchingContentIdea is not null)
        {
            _logger.LogInformation(
                "Deleting matching ContentIdea for rejected idea: Id={Id}, Title='{Title}'",
                matchingContentIdea.Id, matchingContentIdea.Title);
            _dbContext.ContentIdeas.Remove(matchingContentIdea);
        }

        _dbContext.IdeaRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class GenerateIdeasCommandHandler : IRequestHandler<GenerateIdeasCommand>
{
    private readonly ContentOsDbContext _dbContext;
    private readonly IIdeationAgent _ideationAgent;
    private readonly IResearchSearchClient _researchClient;
    private readonly ILogger<GenerateIdeasCommandHandler> _logger;

    public GenerateIdeasCommandHandler(
        ContentOsDbContext dbContext,
        IIdeationAgent ideationAgent,
        IResearchSearchClient researchClient,
        ILogger<GenerateIdeasCommandHandler> logger)
    {
        _dbContext = dbContext;
        _ideationAgent = ideationAgent;
        _researchClient = researchClient;
        _logger = logger;
    }

    public async Task Handle(GenerateIdeasCommand request, CancellationToken cancellationToken)
    {
        // Use the provided niche, or fall back to the first site's niche, or default
        var niche = !string.IsNullOrWhiteSpace(request.Niche) ? request.Niche.Trim() : null;
        var audience = !string.IsNullOrWhiteSpace(request.AudienceDescription) ? request.AudienceDescription.Trim() : null;

        var site = await _dbContext.Sites.FirstOrDefaultAsync(cancellationToken);
        if (site is not null)
        {
            niche ??= site.Niche;
            audience ??= $"Readers interested in practical {site.Niche} guidance";
        }
        niche ??= site?.Niche;
        if (string.IsNullOrWhiteSpace(niche))
            throw new InvalidOperationException("No niche configured. Set a niche in Settings or provide one when generating ideas.");

        audience ??= !string.IsNullOrWhiteSpace(site?.Name) ? $"Readers of {site.Name}" : $"Readers interested in {niche}";

        var context = new ResearchContext
        {
            SiteId = site?.Id ?? Guid.Empty,
            SiteName = site?.Name ?? niche,
            SiteUrl = site?.Domain ?? string.Empty,
            Niche = niche,
            AudienceDescription = audience,
            MaxIdeasToSave = request.MaxIdeas
        };

        // Run research queries based on the niche
        var findings = new List<ResearchFinding>();
        var queries = new[]
        {
            $"{niche} for beginners guide",
            $"common {niche} mistakes to avoid",
            $"best {niche} tips and strategies",
            $"{niche} how to get started step by step"
        };

        foreach (var query in queries)
        {
            try
            {
                var results = await _researchClient.SearchAsync(query, maxResults: 3, cancellationToken: cancellationToken);
                foreach (var result in results.Take(3))
                {
                    findings.Add(new ResearchFinding
                    {
                        ProviderName = result.Domain,
                        SourceTitle = result.Title,
                        SourceUrl = result.Url,
                        TopicSuggestion = result.Title,
                        KeywordSuggestion = context.Niche,
                        Notes = result.Content?.Length > 200 ? result.Content[..200] : result.Content,
                        PainPoint = $"Finding reliable {context.Niche} information"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Research search failed for query: {Query}", query);
            }
        }

        // If no findings came back, create synthetic ones from the niche
        if (findings.Count == 0)
        {
            _logger.LogInformation("No research findings, using synthetic findings for niche: {Niche}", context.Niche);
            findings = new List<ResearchFinding>
            {
                new() { TopicSuggestion = $"How to get started with {context.Niche}", KeywordSuggestion = $"{context.Niche} guide", PainPoint = $"Readers struggle to find clear {context.Niche} guidance", ProviderName = "synthetic" },
                new() { TopicSuggestion = $"Common {context.Niche} mistakes to avoid", KeywordSuggestion = $"{context.Niche} mistakes", PainPoint = $"Beginners make avoidable errors in {context.Niche}", ProviderName = "synthetic" },
                new() { TopicSuggestion = $"Best {context.Niche} strategies that work", KeywordSuggestion = $"best {context.Niche} strategies", PainPoint = $"Readers need proven {context.Niche} approaches", ProviderName = "synthetic" }
            };
        }

        // Generate ideas through the ideation agent
        var candidates = await _ideationAgent.GenerateIdeasAsync(context, findings, cancellationToken);

        _logger.LogInformation("Ideation agent produced {Count} candidate ideas", candidates.Count);

        // Persist each candidate as an IdeaRecord
        foreach (var candidate in candidates)
        {
            var idea = new IdeaRecord
            {
                Id = Guid.NewGuid(),
                SourceWorkflowRunId = null,
                SourceWorkflowDefinitionId = null,
                WorkflowVersion = 1,
                SiteId = context.SiteId == Guid.Empty ? null : context.SiteId,
                IdeaTitle = candidate.Title,
                ReaderProblem = candidate.AudiencePainPoint,
                AudienceType = context.AudienceDescription,
                SearchIntent = candidate.SearchIntent,
                EmotionalTrigger = candidate.WhyNow,
                UniquenessAngle = candidate.RecommendedAngle,
                MonetizationFit = candidate.MonetizationFitScore,
                SeoPotential = candidate.SeoOpportunityScore,
                Difficulty = candidate.CompetitionDifficultyScore,
                PriorityScore = candidate.OverallScore,
                Status = IdeaRecordStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IdeaSnapshotJson = System.Text.Json.JsonSerializer.Serialize(candidate)
            };

            _dbContext.IdeaRecords.Add(idea);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {Count} idea records", candidates.Count);
    }
}

public class RetryWorkflowActionRunCommandHandler : IRequestHandler<RetryWorkflowActionRunCommand, bool>
{
    private readonly WorkflowPipelineOrchestrator _workflowPipelineOrchestrator;

    public RetryWorkflowActionRunCommandHandler(WorkflowPipelineOrchestrator workflowPipelineOrchestrator)
    {
        _workflowPipelineOrchestrator = workflowPipelineOrchestrator;
    }

    public Task<bool> Handle(RetryWorkflowActionRunCommand request, CancellationToken cancellationToken)
    {
        return _workflowPipelineOrchestrator.RetryActionAsync(request.WorkflowDefinitionRunId, request.WorkflowActionRunId, cancellationToken);
    }
}

public class GetWorkflowDefinitionRunQueryHandler : IRequestHandler<GetWorkflowDefinitionRunQuery, WorkflowDefinitionRunDto?>
{
    private readonly ContentOsDbContext _dbContext;

    public GetWorkflowDefinitionRunQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowDefinitionRunDto?> Handle(GetWorkflowDefinitionRunQuery request, CancellationToken cancellationToken)
    {
        var run = await _dbContext.WorkflowDefinitionRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RunId, cancellationToken);

        if (run is null) return null;

        var actions = await _dbContext.WorkflowActionRuns
            .AsNoTracking()
            .Where(x => x.WorkflowDefinitionRunId == run.Id)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        var actionIds = actions.Select(x => x.Id).ToList();
        var steps = await _dbContext.WorkflowStepRuns
            .AsNoTracking()
            .Where(x => actionIds.Contains(x.WorkflowActionRunId))
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        string? ideaTitle = null;
        string? readerProblem = null;
        if (run.IdeaRecordId.HasValue)
        {
            var idea = await _dbContext.IdeaRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.IdeaRecordId.Value, cancellationToken);
            ideaTitle = idea?.IdeaTitle;
            readerProblem = idea?.ReaderProblem;
        }

        return new WorkflowDefinitionRunDto
        {
            Id = run.Id,
            WorkflowDefinitionFamilyId = run.WorkflowDefinitionFamilyId,
            WorkflowDefinitionId = run.WorkflowDefinitionId,
            WorkflowType = run.WorkflowType.ToString(),
            Version = run.Version,
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage,
            IdeaRecordId = run.IdeaRecordId,
            IdeaTitle = ideaTitle,
            ReaderProblem = readerProblem,
            TriggeredBy = run.TriggeredBy,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            Actions = actions.Select(a => new WorkflowActionRunDto
            {
                Id = a.Id,
                Name = a.Name,
                CapabilityKey = a.WorkflowActionDefinitionId != Guid.Empty ? string.Empty : string.Empty,
                Order = a.Order,
                Status = a.Status.ToString(),
                ErrorMessage = a.ErrorMessage,
                RetryCount = a.RetryCount,
                MaxRetry = a.MaxRetry,
                OutputSnapshotJson = a.OutputSnapshotJson,
                StartedUtc = a.StartedUtc,
                CompletedUtc = a.CompletedUtc,
                Steps = steps.Where(s => s.WorkflowActionRunId == a.Id)
                    .Select(s => new WorkflowStepRunDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Order = s.Order,
                        Status = s.Status.ToString(),
                        StartedUtc = s.StartedUtc,
                        CompletedUtc = s.CompletedUtc
                    }).ToList()
            }).ToList()
        };
    }
}

public class GetArticleRunsQueryHandler : IRequestHandler<GetArticleRunsQuery, IEnumerable<WorkflowDefinitionRunDto>>
{
    private readonly ContentOsDbContext _dbContext;

    public GetArticleRunsQueryHandler(ContentOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<WorkflowDefinitionRunDto>> Handle(GetArticleRunsQuery request, CancellationToken cancellationToken)
    {
        var runs = await _dbContext.WorkflowDefinitionRuns
            .AsNoTracking()
            .Where(x => x.WorkflowType == WorkflowDefinitionType.Article)
            .OrderByDescending(x => x.StartedUtc)
            .ToListAsync(cancellationToken);

        var ideaIds = runs.Where(x => x.IdeaRecordId.HasValue).Select(x => x.IdeaRecordId!.Value).Distinct().ToList();
        var ideas = await _dbContext.IdeaRecords.AsNoTracking()
            .Where(x => ideaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return runs.Select(run => new WorkflowDefinitionRunDto
        {
            Id = run.Id,
            WorkflowDefinitionFamilyId = run.WorkflowDefinitionFamilyId,
            WorkflowDefinitionId = run.WorkflowDefinitionId,
            WorkflowType = run.WorkflowType.ToString(),
            Version = run.Version,
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage,
            IdeaRecordId = run.IdeaRecordId,
            IdeaTitle = run.IdeaRecordId.HasValue && ideas.TryGetValue(run.IdeaRecordId.Value, out var idea) ? idea.IdeaTitle : null,
            ReaderProblem = run.IdeaRecordId.HasValue && ideas.TryGetValue(run.IdeaRecordId.Value, out var idea2) ? idea2.ReaderProblem : null,
            TriggeredBy = run.TriggeredBy,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc
        }).ToList();
    }
}
