using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitIdeaArticleWorkflowPipelines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CreateSplitWorkflowTablesOnly(migrationBuilder);
            return;
            migrationBuilder.CreateTable(
                name: "AgentExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AgentName = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MetaDescription = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    CallToAction = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ArtifactType = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StorageType = table.Column<string>(type: "TEXT", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContentText = table.Column<string>(type: "TEXT", nullable: false),
                    BlobPath = table.Column<string>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByAgent = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentIdeas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SiteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SlugSuggestion = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryKeyword = table.Column<string>(type: "TEXT", nullable: false),
                    SecondaryKeywordsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SearchIntent = table.Column<string>(type: "TEXT", nullable: false),
                    AudiencePainPoint = table.Column<string>(type: "TEXT", nullable: false),
                    AudienceGoal = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAngle = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    WhyNow = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    FunnelStage = table.Column<string>(type: "TEXT", nullable: false),
                    MonetizationFitScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    SeoOpportunityScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    TrendScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    CompetitionScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    OverallScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    Evergreen = table.Column<bool>(type: "INTEGER", nullable: false),
                    Seasonal = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TopicType = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificityTag = table.Column<string>(type: "TEXT", nullable: false),
                    IntentMatchScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    UniquenessScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    ClickPotentialScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    MonetizationPotentialScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsHighCompetition = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompetitionModifier = table.Column<decimal>(type: "TEXT", nullable: false),
                    LowCompetitionBoost = table.Column<decimal>(type: "TEXT", nullable: false),
                    MonetizationViable = table.Column<bool>(type: "INTEGER", nullable: false),
                    MonetizationWarning = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentIdeas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentResearchSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentIdeaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceTitle = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentResearchSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentWorkflowJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentIdeaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTemplateVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentStage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentWorkflowJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Domain = table.Column<string>(type: "TEXT", nullable: false),
                    Niche = table.Column<string>(type: "TEXT", nullable: false),
                    PlatformType = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultTone = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArticleIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflow", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionFamilies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveWorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionMutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MutationType = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    OldValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    NewValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionMutations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowMutationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MutationType = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousStatus = table.Column<string>(type: "TEXT", nullable: false),
                    NewStatus = table.Column<string>(type: "TEXT", nullable: false),
                    OldValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    NewValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ActorIdentity = table.Column<string>(type: "TEXT", nullable: false),
                    ActorType = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowMutationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    InstructionsTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredInputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AutoStartWhenPreviousComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsApprovalRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTaskTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleChatThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleChatThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleChatThreads_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    MetaDescription = table.Column<string>(type: "TEXT", nullable: false),
                    CallToAction = table.Column<string>(type: "TEXT", nullable: false),
                    DiffSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleRevisions_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeDescription = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleVersions_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublishRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<string>(type: "TEXT", nullable: false),
                    PublicationChannel = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublishRecord_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentWorkflowTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentWorkflowJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTaskTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<string>(type: "TEXT", nullable: true),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    InputDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InsertedAfterTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultArtifactId = table.Column<string>(type: "TEXT", nullable: true),
                    PatchPayload = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutionAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SupersedesTaskId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentWorkflowTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentWorkflowTasks_ContentWorkflowJobs_ContentWorkflowJobId",
                        column: x => x.ContentWorkflowJobId,
                        principalTable: "ContentWorkflowJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRun_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowRun_Workflow_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflow",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionFamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId",
                        column: x => x.WorkflowDefinitionFamilyId,
                        principalTable: "WorkflowDefinitionFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTemplateTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultInstructions = table.Column<string>(type: "TEXT", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTemplateTask", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTemplateTask_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleEditRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserMessage = table.Column<string>(type: "TEXT", nullable: false),
                    Intent = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSectionId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLabel = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedText = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RoutedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    QaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    QaSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DiffSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedContent = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedMetaDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedCallToAction = table.Column<string>(type: "TEXT", nullable: false),
                    ProposalJson = table.Column<string>(type: "TEXT", nullable: false),
                    LockedFields = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleEditRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleEditRequests_ArticleChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ArticleChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapabilityKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowActionDefinitions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowActionDefinitions_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ArticleChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleChatMessages_ArticleChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ArticleChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleChatMessages_ArticleEditRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ArticleEditRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ArticleEditPatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeContent = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedContent = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: false),
                    Warnings = table.Column<string>(type: "TEXT", nullable: false),
                    FieldLocksRespected = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleEditPatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleEditPatches_ArticleEditRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ArticleEditRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapabilityKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepDefinitions_WorkflowActionDefinitions_WorkflowActionDefinitionId",
                        column: x => x.WorkflowActionDefinitionId,
                        principalTable: "WorkflowActionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdeaRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceWorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceWorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IdeaTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ReaderProblem = table.Column<string>(type: "TEXT", nullable: false),
                    AudienceType = table.Column<string>(type: "TEXT", nullable: false),
                    SearchIntent = table.Column<string>(type: "TEXT", nullable: false),
                    EmotionalTrigger = table.Column<string>(type: "TEXT", nullable: false),
                    UniquenessAngle = table.Column<string>(type: "TEXT", nullable: false),
                    MonetizationFit = table.Column<decimal>(type: "TEXT", nullable: false),
                    SeoPotential = table.Column<decimal>(type: "TEXT", nullable: false),
                    Difficulty = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriorityScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IdeaSnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaRecords_WorkflowDefinitions_SourceWorkflowDefinitionId",
                        column: x => x.SourceWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionFamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggeredBy = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    IdeaRecordId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_IdeaRecords_IdeaRecordId",
                        column: x => x.IdeaRecordId,
                        principalTable: "IdeaRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId",
                        column: x => x.WorkflowDefinitionFamilyId,
                        principalTable: "WorkflowDefinitionFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetry = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFailureUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowActionRuns_WorkflowActionDefinitions_WorkflowActionDefinitionId",
                        column: x => x.WorkflowActionDefinitionId,
                        principalTable: "WorkflowActionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowActionRuns_WorkflowDefinitionRuns_WorkflowDefinitionRunId",
                        column: x => x.WorkflowDefinitionRunId,
                        principalTable: "WorkflowDefinitionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowStepDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepRuns_WorkflowActionRuns_WorkflowActionRunId",
                        column: x => x.WorkflowActionRunId,
                        principalTable: "WorkflowActionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowStepRuns_WorkflowStepDefinitions_WorkflowStepDefinitionId",
                        column: x => x.WorkflowStepDefinitionId,
                        principalTable: "WorkflowStepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleChatMessages_RequestId",
                table: "ArticleChatMessages",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleChatMessages_ThreadId_CreatedAtUtc",
                table: "ArticleChatMessages",
                columns: new[] { "ThreadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleChatThreads_ArticleId",
                table: "ArticleChatThreads",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleEditPatches_RequestId_SortOrder",
                table: "ArticleEditPatches",
                columns: new[] { "RequestId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleEditRequests_ArticleId_CreatedAtUtc",
                table: "ArticleEditRequests",
                columns: new[] { "ArticleId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleEditRequests_ThreadId",
                table: "ArticleEditRequests",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleRevisions_ArticleId_RevisionNumber",
                table: "ArticleRevisions",
                columns: new[] { "ArticleId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleVersions_ArticleId",
                table: "ArticleVersions",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentIdeas_Status",
                table: "ContentIdeas",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentWorkflowJobs_ContentIdeaId",
                table: "ContentWorkflowJobs",
                column: "ContentIdeaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentWorkflowJobs_Status",
                table: "ContentWorkflowJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentWorkflowTasks_ContentWorkflowJobId_DisplayOrder",
                table: "ContentWorkflowTasks",
                columns: new[] { "ContentWorkflowJobId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentWorkflowTasks_Status",
                table: "ContentWorkflowTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaRecords_CreatedUtc",
                table: "IdeaRecords",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaRecords_SourceWorkflowDefinitionId",
                table: "IdeaRecords",
                column: "SourceWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaRecords_SourceWorkflowRunId",
                table: "IdeaRecords",
                column: "SourceWorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaRecords_Status_PriorityScore",
                table: "IdeaRecords",
                columns: new[] { "Status", "PriorityScore" });

            migrationBuilder.CreateIndex(
                name: "IX_PublishRecord_ArticleId",
                table: "PublishRecord",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionDefinitions_WorkflowDefinitionId_Order",
                table: "WorkflowActionDefinitions",
                columns: new[] { "WorkflowDefinitionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionDefinitions_WorkflowTemplateId",
                table: "WorkflowActionDefinitions",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionRuns_WorkflowActionDefinitionId",
                table: "WorkflowActionRuns",
                column: "WorkflowActionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionRuns_WorkflowDefinitionRunId_Order",
                table: "WorkflowActionRuns",
                columns: new[] { "WorkflowDefinitionRunId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionRuns_WorkflowDefinitionRunId_WorkflowActionDefinitionId",
                table: "WorkflowActionRuns",
                columns: new[] { "WorkflowDefinitionRunId", "WorkflowActionDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionFamilies_Name_WorkflowType",
                table: "WorkflowDefinitionFamilies",
                columns: new[] { "Name", "WorkflowType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionMutations_WorkflowDefinitionId_CreatedUtc",
                table: "WorkflowDefinitionMutations",
                columns: new[] { "WorkflowDefinitionId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionRuns_IdeaRecordId",
                table: "WorkflowDefinitionRuns",
                column: "IdeaRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionRuns_WorkflowDefinitionFamilyId",
                table: "WorkflowDefinitionRuns",
                column: "WorkflowDefinitionFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionRuns_WorkflowDefinitionId_StartedUtc",
                table: "WorkflowDefinitionRuns",
                columns: new[] { "WorkflowDefinitionId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionRuns_WorkflowType_Status",
                table: "WorkflowDefinitionRuns",
                columns: new[] { "WorkflowType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_IsActive",
                table: "WorkflowDefinitions",
                columns: new[] { "WorkflowDefinitionFamilyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_Version",
                table: "WorkflowDefinitions",
                columns: new[] { "WorkflowDefinitionFamilyId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowMutationLogs_ContentWorkflowJobId_CreatedUtc",
                table: "WorkflowMutationLogs",
                columns: new[] { "ContentWorkflowJobId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRun_ArticleId",
                table: "WorkflowRun",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRun_WorkflowId",
                table: "WorkflowRun",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepDefinitions_WorkflowActionDefinitionId_Order",
                table: "WorkflowStepDefinitions",
                columns: new[] { "WorkflowActionDefinitionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRuns_WorkflowActionRunId_Order",
                table: "WorkflowStepRuns",
                columns: new[] { "WorkflowActionRunId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRuns_WorkflowActionRunId_WorkflowStepDefinitionId",
                table: "WorkflowStepRuns",
                columns: new[] { "WorkflowActionRunId", "WorkflowStepDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRuns_WorkflowStepDefinitionId",
                table: "WorkflowStepRuns",
                column: "WorkflowStepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTaskTemplates_WorkflowTemplateId_DisplayOrder",
                table: "WorkflowTaskTemplates",
                columns: new[] { "WorkflowTemplateId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTemplates_Name",
                table: "WorkflowTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTemplateTask_WorkflowTemplateId",
                table: "WorkflowTemplateTask",
                column: "WorkflowTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdeaRecords_WorkflowDefinitionRuns_SourceWorkflowRunId",
                table: "IdeaRecords",
                column: "SourceWorkflowRunId",
                principalTable: "WorkflowDefinitionRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void CreateSplitWorkflowTablesOnly(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionFamilies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveWorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionFamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId",
                        column: x => x.WorkflowDefinitionFamilyId,
                        principalTable: "WorkflowDefinitionFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetry = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFailureUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdeaRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceWorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceWorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IdeaTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ReaderProblem = table.Column<string>(type: "TEXT", nullable: false),
                    AudienceType = table.Column<string>(type: "TEXT", nullable: false),
                    SearchIntent = table.Column<string>(type: "TEXT", nullable: false),
                    EmotionalTrigger = table.Column<string>(type: "TEXT", nullable: false),
                    UniquenessAngle = table.Column<string>(type: "TEXT", nullable: false),
                    MonetizationFit = table.Column<decimal>(type: "TEXT", nullable: false),
                    SeoPotential = table.Column<decimal>(type: "TEXT", nullable: false),
                    Difficulty = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriorityScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IdeaSnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaRecords_WorkflowDefinitions_SourceWorkflowDefinitionId",
                        column: x => x.SourceWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionFamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggeredBy = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    IdeaRecordId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_IdeaRecords_IdeaRecordId",
                        column: x => x.IdeaRecordId,
                        principalTable: "IdeaRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId",
                        column: x => x.WorkflowDefinitionFamilyId,
                        principalTable: "WorkflowDefinitionFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionRuns_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapabilityKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowActionDefinitions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapabilityKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepDefinitions_WorkflowActionDefinitions_WorkflowActionDefinitionId",
                        column: x => x.WorkflowActionDefinitionId,
                        principalTable: "WorkflowActionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowActionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowStepDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitionFamilies_Name_WorkflowType", table: "WorkflowDefinitionFamilies", columns: new[] { "Name", "WorkflowType" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_IsActive", table: "WorkflowDefinitions", columns: new[] { "WorkflowDefinitionFamilyId", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_Version", table: "WorkflowDefinitions", columns: new[] { "WorkflowDefinitionFamilyId", "Version" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_WorkflowActionDefinitions_WorkflowDefinitionId_Order", table: "WorkflowActionDefinitions", columns: new[] { "WorkflowDefinitionId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_WorkflowStepDefinitions_WorkflowActionDefinitionId_Order", table: "WorkflowStepDefinitions", columns: new[] { "WorkflowActionDefinitionId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_IdeaRecords_CreatedUtc", table: "IdeaRecords", column: "CreatedUtc");
            migrationBuilder.CreateIndex(name: "IX_IdeaRecords_SourceWorkflowDefinitionId", table: "IdeaRecords", column: "SourceWorkflowDefinitionId");
            migrationBuilder.CreateIndex(name: "IX_IdeaRecords_Status_PriorityScore", table: "IdeaRecords", columns: new[] { "Status", "PriorityScore" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitionRuns_IdeaRecordId", table: "WorkflowDefinitionRuns", column: "IdeaRecordId");
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitionRuns_WorkflowDefinitionFamilyId", table: "WorkflowDefinitionRuns", column: "WorkflowDefinitionFamilyId");
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitionRuns_WorkflowDefinitionId_StartedUtc", table: "WorkflowDefinitionRuns", columns: new[] { "WorkflowDefinitionId", "StartedUtc" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitionRuns_WorkflowType_Status", table: "WorkflowDefinitionRuns", columns: new[] { "WorkflowType", "Status" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowActionRuns_WorkflowDefinitionRunId_Order", table: "WorkflowActionRuns", columns: new[] { "WorkflowDefinitionRunId", "Order" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowActionRuns_WorkflowDefinitionRunId_WorkflowActionDefinitionId", table: "WorkflowActionRuns", columns: new[] { "WorkflowDefinitionRunId", "WorkflowActionDefinitionId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_WorkflowStepRuns_WorkflowActionRunId_Order", table: "WorkflowStepRuns", columns: new[] { "WorkflowActionRunId", "Order" });
            migrationBuilder.CreateIndex(name: "IX_WorkflowStepRuns_WorkflowActionRunId_WorkflowStepDefinitionId", table: "WorkflowStepRuns", columns: new[] { "WorkflowActionRunId", "WorkflowStepDefinitionId" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdeaRecords_WorkflowDefinitionRuns_SourceWorkflowRunId",
                table: "IdeaRecords");

            migrationBuilder.DropTable(
                name: "AgentExecutionLogs");

            migrationBuilder.DropTable(
                name: "ArticleChatMessages");

            migrationBuilder.DropTable(
                name: "ArticleEditPatches");

            migrationBuilder.DropTable(
                name: "ArticleRevisions");

            migrationBuilder.DropTable(
                name: "ArticleVersions");

            migrationBuilder.DropTable(
                name: "ContentArtifacts");

            migrationBuilder.DropTable(
                name: "ContentIdeas");

            migrationBuilder.DropTable(
                name: "ContentResearchSources");

            migrationBuilder.DropTable(
                name: "ContentWorkflowTasks");

            migrationBuilder.DropTable(
                name: "PublishRecord");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionMutations");

            migrationBuilder.DropTable(
                name: "WorkflowMutationLogs");

            migrationBuilder.DropTable(
                name: "WorkflowRun");

            migrationBuilder.DropTable(
                name: "WorkflowStepRuns");

            migrationBuilder.DropTable(
                name: "WorkflowTaskTemplates");

            migrationBuilder.DropTable(
                name: "WorkflowTemplateTask");

            migrationBuilder.DropTable(
                name: "ArticleEditRequests");

            migrationBuilder.DropTable(
                name: "ContentWorkflowJobs");

            migrationBuilder.DropTable(
                name: "Workflow");

            migrationBuilder.DropTable(
                name: "WorkflowActionRuns");

            migrationBuilder.DropTable(
                name: "WorkflowStepDefinitions");

            migrationBuilder.DropTable(
                name: "ArticleChatThreads");

            migrationBuilder.DropTable(
                name: "WorkflowActionDefinitions");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "WorkflowTemplates");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionRuns");

            migrationBuilder.DropTable(
                name: "IdeaRecords");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionFamilies");
        }
    }
}
