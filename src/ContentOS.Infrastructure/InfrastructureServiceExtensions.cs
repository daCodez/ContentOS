using ContentOS.Domain.Repositories;
using ContentOS.Application.Abstractions;
using ContentOS.Infrastructure.Ideation;
using ContentOS.Infrastructure.Image;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Research.Providers;
using ContentOS.Infrastructure.Research.Serp;
using ContentOS.Infrastructure.Research.SeoData;
using ContentOS.Infrastructure.Research.Abstractions;
using ContentOS.Infrastructure.Research.SearXng;
using ContentOS.Infrastructure.Research.Extraction;
using ContentOS.Infrastructure.Research.Retrieval;
using ContentOS.Infrastructure.Scoring;
using ContentOS.Infrastructure.Writing;
using ContentOS.Infrastructure.Articles;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ContentOS.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all infrastructure services including DbContext.
    /// The connection string MUST be provided explicitly — no silent fallback to a relative path.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string connectionString, ILogger? startupLogger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ContentOS connection string must be provided explicitly. Refusing to fall back to a relative SQLite path.");

        // Guardrail: detect if DbContext was already registered with a different connection string
        var existingDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DbContextOptions<ContentOsDbContext>));
        if (existingDescriptor is not null)
        {
            // DbContext already registered — check if it's pointing to the same DB
            // Since we can't easily inspect the internal connection string, log a warning
            startupLogger?.LogWarning("DbContextOptions<ContentOsDbContext> is already registered. The first registration wins. Ensure all registrations use the same connection string: {ConnectionString}", connectionString);
        }

        startupLogger?.LogInformation("[ContentOS] Resolved DB path: {ConnectionString}", connectionString);

        services.AddDbContext<ContentOsDbContext>(options =>
            options.UseSqlite(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // Use SearXNG instead of Tavily for search
        services.AddHttpClient<SearXngContentExtractor>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddSingleton<SearXngContentExtractor>();
        services.AddHttpClient<SearXngStructuredExtractor>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddSingleton<SearXngStructuredExtractor>();
        services.AddSingleton<IContentExtractor, ContentExtractorAdapter>();
        services.AddSingleton<IRetrievalService, QmdRetrievalService>();
        services.AddHttpClient<IResearchSearchClient, SearXngResearchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IEditorialExemplarService, EditorialExemplarService>();
        services.AddHttpClient<IWorkflowArticleWriter, OllamaWorkflowArticleWriter>(client =>
        {
            client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("CONTENTOS_WRITER_BASE_URL") ?? "http://127.0.0.1:11434/");
            client.Timeout = TimeSpan.FromMinutes(4);
        });

        services.AddSingleton<IArticleRepository, InMemoryArticleRepository>();
        services.AddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();
        services.AddHttpClient<IImageGenerationService, OpenAiImageGenerationService>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddScoped<IIdeationAgent, IdeationAgent>();
        services.AddScoped<IResearchAgent, ResearchAgent>();
        services.AddScoped<ISerpBenchmarkService, SerpBenchmarkService>();
        services.AddScoped<IOptimizationScoringService, DefaultOptimizationScoringService>();
        services.AddScoped<ISeparateIdeaReviewer, SeparateIdeaReviewer>();
        services.AddScoped<IResearchSourceProvider, RedditResearchProvider>();
        services.AddScoped<IResearchSourceProvider, SearchIntentResearchProvider>();
        services.AddScoped<IResearchSourceProvider, CompetitorResearchProvider>();
        services.AddScoped<IResearchSourceProvider, MonetizationResearchProvider>();
        services.AddScoped<IResearchSourceProvider, TrendResearchProvider>();
        services.AddScoped<WorkflowPipelineOrchestrator>();
        services.AddScoped<FullWorkflowSpecificationStore>();
        services.AddScoped<FullWorkflowStepRuntime>();
        foreach (var capability in IdeaWorkflowStepHandler.Capabilities)
        {
            services.AddScoped<IFullWorkflowStepHandler>(provider => new IdeaWorkflowStepHandler(capability,
                provider.GetRequiredService<ContentOsDbContext>(), provider.GetServices<IResearchSourceProvider>(),
                provider.GetRequiredService<IIdeationAgent>(), provider.GetRequiredService<IResearchSearchClient>(),
                provider.GetRequiredService<IIdeaDeduplicator>(), provider.GetRequiredService<ISeparateIdeaReviewer>()));
        }
        services.AddScoped<NewWorkflowRuntimeDispatcher>();
        foreach (var capability in ArticleDeliveryWorkflowStepHandler.Capabilities)
        {
            services.AddScoped<IFullWorkflowStepHandler>(provider => new ArticleDeliveryWorkflowStepHandler(capability,
                provider.GetRequiredService<IWorkflowArticleWriter>(),provider.GetService<IArticleEvidenceReviewer>(),
                provider.GetService<IAuthorizedArticleAssetProvider>()));
        }
        services.AddSingleton<IArticlePublicPageReader, ArticlePublicPageReader>();
        services.AddScoped<IArticleEvidenceReviewer, SourceBoundArticleEvidenceReviewer>();
        foreach (var capability in ArticlePlanningWorkflowStepHandler.Capabilities)
        {
            services.AddScoped<IFullWorkflowStepHandler>(provider => new ArticlePlanningWorkflowStepHandler(capability,
                provider.GetRequiredService<ContentOsDbContext>(),provider.GetRequiredService<ILlmClient>(),
                provider.GetRequiredService<IWorkflowArticleWriter>(),provider.GetRequiredService<IResearchSearchClient>(),
                provider.GetService<IArticleEvidenceReviewer>(),provider.GetService<IPublishedArticleInventory>()));
        }
        services.AddScoped<ContentOS.Application.Abstractions.IWorkflowBootstrapService, WorkflowBootstrapService>();
        services.AddScoped<ContentOS.Infrastructure.Agents.IContentStrategyAgent, ContentOS.Infrastructure.Agents.ContentStrategyAgent>();
        services.AddHttpClient<ContentOS.Application.Abstractions.ILlmClient, ContentOS.Infrastructure.Agents.OllamaLlmClient>(client =>
        {
            client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("CONTENTOS_WRITER_BASE_URL") ?? "http://127.0.0.1:11434/");
            client.Timeout = TimeSpan.FromMinutes(4);
        });

        // Register file storage service
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.Configure<ContentOS.Application.Configuration.SimulationSettings>(opt => { opt.UseSimulatedAgents = false; });
        services.AddScoped<ContentOS.Infrastructure.Agents.IEditorialAgent, ContentOS.Infrastructure.Agents.EditorialAgent>();
        services.AddScoped<ContentOS.Infrastructure.Agents.ISeoAndMonetizationAgent, ContentOS.Infrastructure.Agents.SeoAndMonetizationAgent>();
        services.AddScoped<ContentOS.Infrastructure.Agents.IQaAndComplianceAgent, ContentOS.Infrastructure.Agents.QaAndComplianceAgent>();
        services.AddScoped<ContentOS.Infrastructure.Agents.IQaFixVerificationAgent, ContentOS.Infrastructure.Agents.QaFixVerificationAgent>();
        services.AddScoped<ContentOS.Infrastructure.Agents.ILightQaAgent, ContentOS.Infrastructure.Agents.LightQaAgent>();
        services.AddScoped<ContentOS.Infrastructure.Ideation.ITopicDiversityScorer, ContentOS.Infrastructure.Ideation.TopicDiversityScorer>();
        services.AddScoped<ContentOS.Infrastructure.Research.IIdeaDeduplicator, ContentOS.Infrastructure.Research.IdeaDeduplicatorService>();
        services.AddScoped<ContentOS.Infrastructure.Agents.ISeoOptimizationAgent, ContentOS.Infrastructure.Agents.SeoOptimizationAgent>();
        services.AddScoped<ContentOS.Infrastructure.Agents.IWorkflowTaskDefinitionResolver, ContentOS.Infrastructure.Agents.WorkflowTaskDefinitionResolver>();
        services.AddScoped<INewWorkflowRuntimeEngine, NewWorkflowRuntimeEngine>();
        // Register null notification service as a fallback so GetRequiredService/IWorkflowNotificationService never throws.
        // Host projects (Web, Api) can override with a real implementation before or after this call.
        if (!services.Any(s => s.ServiceType == typeof(ContentOS.Application.Notifications.IWorkflowNotificationService)))
        {
            services.AddSingleton<ContentOS.Application.Notifications.IWorkflowNotificationService, ContentOS.Application.Notifications.NullWorkflowNotificationService>();
        }
        services.AddScoped<ContentOS.Infrastructure.Agents.IWorkflowTaskDispatcher, ContentOS.Infrastructure.Agents.WorkflowTaskDispatcher>();
        services.AddScoped<ContentOS.Infrastructure.Agents.IWorkflowCoordinatorAgent, ContentOS.Infrastructure.Agents.WorkflowCoordinatorAgent>();
        services.AddScoped<IWorkflowExportService, WorkflowExportService>();
        services.AddScoped<IWorkflowMutationService, WorkflowMutationService>();
        services.AddScoped<IWorkflowControlService, WorkflowControlService>();
        services.AddScoped<IArticleEditingService, ArticleEditingService>();
        services.AddScoped<IArticleEditRunner, WriterArticleEditRunner>();
        services.AddScoped<IArticleEditRunner, HumanizerArticleEditRunner>();
        services.AddScoped<IArticleEditRunner, SeoArticleEditRunner>();
        services.AddScoped<IArticleEditRunner, MonetizationArticleEditRunner>();
        services.AddScoped<IArticleEditRunner, QaReviewArticleEditRunner>();
        services.AddHostedService<WorkflowCoordinatorBackgroundService>();
        services.AddSeoDataProviders();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        return services;
    }
}
