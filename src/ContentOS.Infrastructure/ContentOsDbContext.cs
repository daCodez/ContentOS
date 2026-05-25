using ContentOS.Domain.Entities;
using ContentOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContentOS.Infrastructure;

public class ContentOsDbContext : DbContext
{
    public ContentOsDbContext(DbContextOptions<ContentOsDbContext> options) : base(options)
    {
    }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<ContentIdea> ContentIdeas => Set<ContentIdea>();
    public DbSet<ContentResearchSource> ContentResearchSources => Set<ContentResearchSource>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowTaskTemplate> WorkflowTaskTemplates => Set<WorkflowTaskTemplate>();
    public DbSet<WorkflowDefinitionFamily> WorkflowDefinitionFamilies => Set<WorkflowDefinitionFamily>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowActionDefinition> WorkflowActionDefinitions => Set<WorkflowActionDefinition>();
    public DbSet<WorkflowStepDefinition> WorkflowStepDefinitions => Set<WorkflowStepDefinition>();
    public DbSet<WorkflowDefinitionMutation> WorkflowDefinitionMutations => Set<WorkflowDefinitionMutation>();
    public DbSet<WorkflowDefinitionRun> WorkflowDefinitionRuns => Set<WorkflowDefinitionRun>();
    public DbSet<WorkflowActionRun> WorkflowActionRuns => Set<WorkflowActionRun>();
    public DbSet<WorkflowStepRun> WorkflowStepRuns => Set<WorkflowStepRun>();
    public DbSet<IdeaRecord> IdeaRecords => Set<IdeaRecord>();
    public DbSet<ContentWorkflowJob> ContentWorkflowJobs => Set<ContentWorkflowJob>();
    public DbSet<ContentWorkflowTask> ContentWorkflowTasks => Set<ContentWorkflowTask>();
    public DbSet<ContentArtifact> ContentArtifacts => Set<ContentArtifact>();
    public DbSet<WorkflowMutationLog> WorkflowMutationLogs => Set<WorkflowMutationLog>();
    public DbSet<AgentExecutionLog> AgentExecutionLogs => Set<AgentExecutionLog>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<ArticleChatThread> ArticleChatThreads => Set<ArticleChatThread>();
    public DbSet<ArticleChatMessage> ArticleChatMessages => Set<ArticleChatMessage>();
    public DbSet<ArticleEditRequest> ArticleEditRequests => Set<ArticleEditRequest>();
    public DbSet<ArticleEditPatch> ArticleEditPatches => Set<ArticleEditPatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Site>().HasKey(x => x.Id);
        modelBuilder.Entity<ContentIdea>().HasKey(x => x.Id);
        modelBuilder.Entity<ContentResearchSource>().HasKey(x => x.Id);
        modelBuilder.Entity<WorkflowTemplate>().HasKey(x => x.Id);
        modelBuilder.Entity<WorkflowTaskTemplate>().HasKey(x => x.Id);
        modelBuilder.Entity<ContentWorkflowJob>().HasKey(x => x.Id);
        modelBuilder.Entity<ContentWorkflowTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ContentWorkflowJob>()
                .WithMany()
                .HasForeignKey(e => e.ContentWorkflowJobId);
            
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Kind).HasConversion<int>();
            entity.Property(e => e.ScopeType).HasConversion<int>();
        });

        modelBuilder.Entity<WorkflowTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<WorkflowDefinitionFamily>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.WorkflowType }).IsUnique();
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkflowType).HasConversion<int>();
            entity.HasOne<WorkflowDefinitionFamily>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionFamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkflowDefinitionFamilyId, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.WorkflowDefinitionFamilyId, e.IsActive });
        });

        modelBuilder.Entity<WorkflowTemplateTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<WorkflowTemplate>()
                .WithMany(w => w.Tasks)
                .HasForeignKey(e => e.WorkflowTemplateId);
        });

        modelBuilder.Entity<WorkflowActionDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<WorkflowDefinition>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.Order }).IsUnique();
        });

        modelBuilder.Entity<WorkflowStepDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<WorkflowActionDefinition>()
                .WithMany(a => a.Steps)
                .HasForeignKey(e => e.WorkflowActionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkflowActionDefinitionId, e.Order }).IsUnique();
        });

        modelBuilder.Entity<WorkflowDefinitionRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkflowType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne<WorkflowDefinitionFamily>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionFamilyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdeaRecord>()
                .WithMany()
                .HasForeignKey(e => e.IdeaRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.StartedUtc });
            entity.HasIndex(e => new { e.WorkflowType, e.Status });
        });

        modelBuilder.Entity<WorkflowActionRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne<WorkflowDefinitionRun>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<WorkflowActionDefinition>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowActionDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.WorkflowDefinitionRunId, e.WorkflowActionDefinitionId }).IsUnique();
            entity.HasIndex(e => new { e.WorkflowDefinitionRunId, e.Order });
        });

        modelBuilder.Entity<WorkflowStepRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne<WorkflowActionRun>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowActionRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<WorkflowStepDefinition>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowStepDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.WorkflowActionRunId, e.WorkflowStepDefinitionId }).IsUnique();
            entity.HasIndex(e => new { e.WorkflowActionRunId, e.Order });
        });

        modelBuilder.Entity<IdeaRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne<WorkflowDefinitionRun>()
                .WithMany()
                .HasForeignKey(e => e.SourceWorkflowRunId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>()
                .WithMany()
                .HasForeignKey(e => e.SourceWorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.Status, e.PriorityScore });
            entity.HasIndex(e => e.CreatedUtc);
        });

        modelBuilder.Entity<WorkflowDefinitionMutation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.CreatedUtc });
        });

        modelBuilder.Entity<WorkflowTemplate>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<WorkflowTaskTemplate>()
            .HasIndex(x => new { x.WorkflowTemplateId, x.DisplayOrder });

        modelBuilder.Entity<ContentWorkflowJob>()
            .HasIndex(x => x.ContentIdeaId)
            .IsUnique();

        modelBuilder.Entity<WorkflowMutationLog>().HasKey(x => x.Id);
        modelBuilder.Entity<WorkflowMutationLog>()
            .HasIndex(x => new { x.ContentWorkflowJobId, x.CreatedUtc });

        modelBuilder.Entity<ContentWorkflowTask>()
            .HasIndex(x => new { x.ContentWorkflowJobId, x.DisplayOrder });

        modelBuilder.Entity<ContentIdea>().HasIndex(x => x.Status);
        modelBuilder.Entity<ContentWorkflowJob>().HasIndex(x => x.Status);
        modelBuilder.Entity<ContentWorkflowTask>().HasIndex(x => x.Status);
        modelBuilder.Entity<ArticleRevision>().HasIndex(x => new { x.ArticleId, x.RevisionNumber }).IsUnique();
        modelBuilder.Entity<ArticleChatThread>().HasIndex(x => x.ArticleId);
        modelBuilder.Entity<ArticleChatMessage>().HasIndex(x => new { x.ThreadId, x.CreatedAtUtc });
        modelBuilder.Entity<ArticleEditRequest>().HasIndex(x => new { x.ArticleId, x.CreatedAtUtc });
        modelBuilder.Entity<ArticleEditPatch>().HasIndex(x => new { x.RequestId, x.SortOrder });

        modelBuilder.Entity<ArticleVersion>()
            .HasOne(x => x.Article)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.ArticleId);

        modelBuilder.Entity<ArticleChatThread>()
            .HasOne<Article>()
            .WithMany(x => x.ChatThreads)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticleChatMessage>()
            .HasOne(x => x.Thread)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticleEditRequest>()
            .HasOne(x => x.Thread)
            .WithMany(x => x.EditRequests)
            .HasForeignKey(x => x.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticleEditPatch>()
            .HasOne(x => x.Request)
            .WithMany(x => x.Patches)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Article>()
            .Property(x => x.MetaDescription)
            .HasDefaultValue(string.Empty);

        modelBuilder.Entity<Article>()
            .Property(x => x.CallToAction)
            .HasDefaultValue(string.Empty);
    }
}
