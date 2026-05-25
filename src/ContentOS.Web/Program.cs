using ContentOS.Web.Components;
using ContentOS.Web.Diagnostics;
using ContentOS.Application;
using ContentOS.Application.DTOs;
using ContentOS.Infrastructure;
using ContentOS.ServiceDefaults;
using MediatR;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

builder.Services.AddApplicationServices();
var connectionString = builder.Configuration.GetConnectionString("ContentOs")
    ?? throw new InvalidOperationException("Connection string 'ContentOs' not found in configuration.");
builder.Services.AddInfrastructureServices(connectionString);

var app = builder.Build();

// Seed workflow templates on startup if missing
using (var seedScope = app.Services.CreateScope())
{
    var dbContext = seedScope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.ContentOsDbContext>();
    await ContentOS.Infrastructure.WorkflowTemplateSeeder.SeedAsync(dbContext);
}

app.MapPost("/api/sites/{id:guid}", async (Guid id, UpdateSiteRequest body, IMediator mediator, CancellationToken ct) =>
{
    try
    {
        var site = await mediator.Send(new ContentOS.Application.Commands.UpdateSiteCommand(id, body.Name, body.Domain, body.Niche, body.DefaultTone, body.PlatformType, body.IsActive), ct);
        return Results.Ok(new { isSuccessful = true, data = site, message = "Site updated." });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { isSuccessful = false, message = ex.Message });
    }
});

app.MapDelete("/api/sites/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var deleted = await mediator.Send(new ContentOS.Application.Commands.DeleteSiteCommand(id), ct);
    return deleted ? Results.Ok(new { isSuccessful = true, message = "Site deleted." }) : Results.NotFound(new { isSuccessful = false, message = "Site not found." });
});

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/api/sites", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetSitesQuery(), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapPost("/api/sites", async (ContentOS.Application.Commands.CreateSiteCommand command, IMediator mediator, CancellationToken ct) =>
{
    var site = await mediator.Send(command, ct);
    return Results.Ok(new { isSuccessful = true, data = site, message = "Site created." });
});

app.MapGet("/api/content-ideas", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetContentIdeasQuery(), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapPost("/api/content-ideas", async (ContentOS.Application.Commands.CreateContentIdeaCommand command, IMediator mediator, CancellationToken ct) =>
{
    var contentIdeaId = await mediator.Send(command, ct);
    return Results.Ok(new { isSuccessful = true, data = new { contentIdeaId }, message = "Content idea created." });
});

app.MapPost("/api/content-ideas/{id:guid}/approve", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var workflowJobId = await mediator.Send(new ContentOS.Application.Commands.ApproveContentIdeaCommand(id, "Eric"), ct);
    return Results.Ok(new { isSuccessful = true, data = new { workflowJobId }, message = "Content idea approved." });
});

app.MapPost("/api/content-ideas/research/run/{siteId:guid}", async (Guid siteId, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Commands.RunResearchCommand(siteId), ct);
    return Results.Ok(new { isSuccessful = true, data = result, message = "Research run completed." });
});

app.MapPost("/api/content-ideas/{id:guid}/archive", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new ContentOS.Application.Commands.ArchiveContentIdeaCommand(id), ct);
    return Results.Ok(new { isSuccessful = true, data = new { contentIdeaId = id }, message = "Content idea archived." });
});

app.MapDelete("/api/content-ideas/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new ContentOS.Application.Commands.DeleteContentIdeaCommand(id), ct);
    return Results.Ok(new { isSuccessful = true, data = new { contentIdeaId = id }, message = "Content idea deleted." });
});

app.MapGet("/api/workflows/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowJobByIdQuery(id), ct);
    return result == null ? Results.UnprocessableEntity(new { isSuccessful = false, message = "Workflow not found" })
                           : Results.Ok(new { isSuccessful = true, data = result });
});

app.MapGet("/api/workflow-definition-families", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowDefinitionFamiliesQuery(), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapGet("/api/workflow-definitions", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowDefinitionsQuery(), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapGet("/api/workflow-definitions/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowDefinitionByIdQuery(id), ct);
    return result is null ? Results.NotFound(new { isSuccessful = false, message = "Workflow definition not found" })
        : Results.Ok(new { isSuccessful = true, data = result });
});

app.MapPost("/api/workflow-definitions/{id:guid}/proposals", async (Guid id, WorkflowDefinitionProposalRequest body, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Commands.ProposeWorkflowDefinitionChangeCommand(id, body.UserRequest), ct);
    return Results.Ok(new { isSuccessful = true, data = result, message = "Workflow definition change proposed." });
});

app.MapPost("/api/workflow-definitions/{id:guid}/apply", async (Guid id, WorkflowDefinitionApplyRequest body, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Commands.ApplyWorkflowDefinitionChangeCommand(id, body.Proposal, body.CreatedBy), ct);
    return Results.Ok(new { isSuccessful = true, data = result, message = "Workflow definition updated." });
});

app.MapPost("/api/workflow-definitions/{id:guid}/save", async (Guid id, WorkflowDefinitionSaveRequest body, IMediator mediator, CancellationToken ct) =>
{
    if (id != body.Definition.Id)
    {
        return Results.BadRequest(new { isSuccessful = false, message = "Workflow definition id mismatch." });
    }

    var result = await mediator.Send(new ContentOS.Application.Commands.SaveWorkflowDefinitionCommand(body.Definition, body.CreatedBy), ct);
    return Results.Ok(new { isSuccessful = true, data = result, message = "Workflow draft normalized." });
});

app.MapPost("/api/workflow-definitions/{id:guid}/publish", async (Guid id, WorkflowDefinitionSaveRequest body, IMediator mediator, CancellationToken ct) =>
{
    if (id != body.Definition.Id)
    {
        return Results.BadRequest(new { isSuccessful = false, message = "Workflow definition id mismatch." });
    }

    var result = await mediator.Send(new ContentOS.Application.Commands.PublishWorkflowDefinitionVersionCommand(body.Definition, body.CreatedBy), ct);
    return Results.Ok(new { isSuccessful = true, data = result, message = "Workflow definition published." });
});

app.MapGet("/api/workflow-definitions/{id:guid}/history", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowDefinitionHistoryQuery(id), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapGet("/api/workflows/{id:guid}/definition-version", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetWorkflowJobVersionInfoQuery(id), ct);
    return result is null ? Results.NotFound(new { isSuccessful = false, message = "Workflow job not found." })
        : Results.Ok(new { isSuccessful = true, data = result });
});

app.MapGet("/api/idea-queue", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ContentOS.Application.Queries.GetIdeaQueueQuery(), ct);
    return Results.Ok(new { isSuccessful = true, data = result });
});

app.MapPost("/api/idea-records/{id:guid}/approve", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var runId = await mediator.Send(new ContentOS.Application.Commands.ApproveIdeaRecordCommand(id, "Eric"), ct);
    return Results.Ok(new { isSuccessful = true, data = new { workflowRunId = runId }, message = "Idea approved." });
});

app.MapPost("/api/idea-records/{id:guid}/reject", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new ContentOS.Application.Commands.RejectIdeaRecordCommand(id, "Eric"), ct);
    return Results.Ok(new { isSuccessful = true, data = new { ideaRecordId = id }, message = "Idea rejected." });
});

app.MapGet("/api/workflows/{id:guid}/artifacts", ArtifactDump.GetWorkflowArtifacts);

app.MapGet("/api/articles/{id:guid}/editing-session", async (Guid id, IServiceProvider services, CancellationToken ct) =>
{
    try
    {
        using var scope = services.CreateScope();
        var editingService = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.Articles.IArticleEditingService>();
        var result = await editingService.GetSessionAsync(id, ct);
        return Results.Ok(new { isSuccessful = true, data = result });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Article editing session failed: {ex.Message}");
    }
});

app.MapPost("/api/articles/{id:guid}/chat-edit", async (Guid id, ContentOS.Application.Commands.ArticleChatEditCommand body, IServiceProvider services, CancellationToken ct) =>
{
    try
    {
        using var scope = services.CreateScope();
        var editingService = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.Articles.IArticleEditingService>();
        var result = await editingService.CreateEditProposalAsync(id, body.Message, body.Scope, body.TargetSectionId, body.SelectedText, ct);
        return Results.Ok(new { isSuccessful = true, data = result, message = "Edit proposal created." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Edit proposal failed: {ex.Message}");
    }
});

app.MapPost("/api/articles/{id:guid}/edits/{requestId:guid}/accept", async (Guid id, Guid requestId, IServiceProvider services, CancellationToken ct) =>
{
    try
    {
        using var scope = services.CreateScope();
        var editingService = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.Articles.IArticleEditingService>();
        await editingService.AcceptEditAsync(id, requestId, "Eric", ct);
        return Results.Ok(new { isSuccessful = true, message = "Edit applied." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Accept edit failed: {ex.Message}");
    }
});

app.MapPost("/api/articles/{id:guid}/edits/{requestId:guid}/reject", async (Guid id, Guid requestId, IServiceProvider services, CancellationToken ct) =>
{
    try
    {
        using var scope = services.CreateScope();
        var editingService = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.Articles.IArticleEditingService>();
        await editingService.RejectEditAsync(id, requestId, "Eric", ct);
        return Results.Ok(new { isSuccessful = true, message = "Edit rejected." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Reject edit failed: {ex.Message}");
    }
});

app.MapPost("/api/articles/{id:guid}/field-locks", async (Guid id, ContentOS.Application.DTOs.ArticleFieldLocksDto locks, IServiceProvider services, CancellationToken ct) =>
{
    try
    {
        using var scope = services.CreateScope();
        var editingService = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.Articles.IArticleEditingService>();
        var result = await editingService.UpdateFieldLocksAsync(id, locks, ct);
        return Results.Ok(new { isSuccessful = true, data = result, message = "Field locks updated." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Update field locks failed: {ex.Message}");
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

internal sealed record WorkflowDefinitionProposalRequest(string UserRequest);
internal sealed record WorkflowDefinitionApplyRequest(WorkflowDefinitionChangeProposalDto Proposal, string CreatedBy);
internal sealed record WorkflowDefinitionSaveRequest(WorkflowDefinitionDto Definition, string CreatedBy);
internal sealed record UpdateSiteRequest(string Name, string Domain, string Niche, string DefaultTone, string PlatformType, bool IsActive);
