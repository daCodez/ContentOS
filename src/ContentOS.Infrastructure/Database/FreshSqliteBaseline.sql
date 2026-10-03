-- Fixed empty-database baseline generated from the EF model on 2026-09-16.
-- Recovery baseline of the 2026-09-16 current model, not the historical migration schema.
-- Records only migration 20260419003557_SplitIdeaArticleWorkflowPipelines; future migrations must run normally.
CREATE TABLE "AgentExecutionLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AgentExecutionLogs" PRIMARY KEY,
    "ContentWorkflowJobId" TEXT NOT NULL,
    "ContentWorkflowTaskId" TEXT NULL,
    "AgentName" TEXT NOT NULL,
    "Level" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "DetailsJson" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL
);


CREATE TABLE "Articles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Articles" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Author" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "IsPublished" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "MetaDescription" TEXT NOT NULL DEFAULT '',
    "CallToAction" TEXT NOT NULL DEFAULT ''
);


CREATE TABLE "ContentArtifacts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ContentArtifacts" PRIMARY KEY,
    "ContentWorkflowJobId" TEXT NOT NULL,
    "ContentWorkflowTaskId" TEXT NULL,
    "ArtifactType" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "StorageType" TEXT NOT NULL,
    "ContentJson" TEXT NOT NULL,
    "ContentText" TEXT NOT NULL,
    "BlobPath" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "CreatedByAgent" TEXT NOT NULL
);


CREATE TABLE "ContentIdeas" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ContentIdeas" PRIMARY KEY,
    "SiteId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "SlugSuggestion" TEXT NOT NULL,
    "PrimaryKeyword" TEXT NOT NULL,
    "SecondaryKeywordsJson" TEXT NOT NULL,
    "SearchIntent" TEXT NOT NULL,
    "AudiencePainPoint" TEXT NOT NULL,
    "AudienceGoal" TEXT NOT NULL,
    "RecommendedAngle" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "WhyNow" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "FunnelStage" TEXT NOT NULL,
    "MonetizationFitScore" TEXT NOT NULL,
    "SeoOpportunityScore" TEXT NOT NULL,
    "TrendScore" TEXT NOT NULL,
    "CompetitionScore" TEXT NOT NULL,
    "OverallScore" TEXT NOT NULL,
    "Evergreen" INTEGER NOT NULL,
    "Seasonal" INTEGER NOT NULL,
    "SourceSummaryJson" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "TopicType" TEXT NOT NULL,
    "SpecificityTag" TEXT NOT NULL,
    "IntentMatchScore" TEXT NOT NULL,
    "UniquenessScore" TEXT NOT NULL,
    "ClickPotentialScore" TEXT NOT NULL,
    "MonetizationPotentialScore" TEXT NOT NULL,
    "CanonicalTopic" TEXT NOT NULL,
    "Angle" TEXT NOT NULL,
    "Intent" TEXT NOT NULL,
    "PainPoint" TEXT NOT NULL,
    "IsHighCompetition" INTEGER NOT NULL,
    "CompetitionModifier" TEXT NOT NULL,
    "LowCompetitionBoost" TEXT NOT NULL,
    "MonetizationViable" INTEGER NOT NULL,
    "MonetizationWarning" TEXT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    "ApprovedUtc" TEXT NULL,
    "RejectedUtc" TEXT NULL,
    "ApprovedBy" TEXT NULL
);


CREATE TABLE "ContentResearchSources" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ContentResearchSources" PRIMARY KEY,
    "ContentIdeaId" TEXT NOT NULL,
    "SourceType" TEXT NOT NULL,
    "SourceTitle" TEXT NOT NULL,
    "SourceUrl" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL
);


CREATE TABLE "ContentWorkflowJobs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ContentWorkflowJobs" PRIMARY KEY,
    "ContentIdeaId" TEXT NOT NULL,
    "WorkflowTemplateId" TEXT NOT NULL,
    "WorkflowTemplateVersion" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "CurrentStage" TEXT NOT NULL,
    "StartedUtc" TEXT NULL,
    "CompletedUtc" TEXT NULL,
    "LastUpdatedUtc" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL
);


CREATE TABLE "Sites" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Sites" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Domain" TEXT NOT NULL,
    "Niche" TEXT NOT NULL,
    "PlatformType" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "DefaultTone" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL
);


CREATE TABLE "Workflow" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workflow" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    "ArticleIds" TEXT NOT NULL
);


CREATE TABLE "WorkflowDefinitionFamilies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowDefinitionFamilies" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "WorkflowType" INTEGER NOT NULL,
    "Description" TEXT NOT NULL,
    "ActiveWorkflowDefinitionId" TEXT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL
);


CREATE TABLE "WorkflowDefinitionMutations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowDefinitionMutations" PRIMARY KEY,
    "WorkflowDefinitionId" TEXT NOT NULL,
    "MutationType" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "OldValueJson" TEXT NOT NULL,
    "NewValueJson" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL
);


CREATE TABLE "WorkflowMutationLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowMutationLogs" PRIMARY KEY,
    "ContentWorkflowJobId" TEXT NOT NULL,
    "ContentWorkflowTaskId" TEXT NULL,
    "MutationType" TEXT NOT NULL,
    "PreviousStatus" TEXT NOT NULL,
    "NewStatus" TEXT NOT NULL,
    "OldValuesJson" TEXT NOT NULL,
    "NewValuesJson" TEXT NOT NULL,
    "ActorIdentity" TEXT NOT NULL,
    "ActorType" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "CorrelationId" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL
);


CREATE TABLE "WorkflowTaskTemplates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowTaskTemplates" PRIMARY KEY,
    "WorkflowTemplateId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "StageName" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "AssignedAgent" TEXT NOT NULL,
    "InstructionsTemplate" TEXT NOT NULL,
    "RequiredInputsJson" TEXT NOT NULL,
    "ExpectedOutputsJson" TEXT NOT NULL,
    "AutoStartWhenPreviousComplete" INTEGER NOT NULL,
    "IsApprovalRequired" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL
);


CREATE TABLE "WorkflowTemplates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowTemplates" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "LastUpdatedUtc" TEXT NOT NULL
);


CREATE TABLE "ArticleChatThreads" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleChatThreads" PRIMARY KEY,
    "ArticleId" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_ArticleChatThreads_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ArticleRevisions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleRevisions" PRIMARY KEY,
    "ArticleId" TEXT NOT NULL,
    "RequestId" TEXT NULL,
    "RevisionNumber" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "MetaDescription" TEXT NOT NULL,
    "CallToAction" TEXT NOT NULL,
    "DiffSummary" TEXT NOT NULL,
    "ChangedBy" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_ArticleRevisions_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ArticleVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleVersions" PRIMARY KEY,
    "ArticleId" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    "Content" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "ChangeDescription" TEXT NOT NULL,
    CONSTRAINT "FK_ArticleVersions_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PublishRecord" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PublishRecord" PRIMARY KEY,
    "ArticleId" TEXT NOT NULL,
    "PublishedAt" TEXT NOT NULL,
    "PublishedBy" TEXT NOT NULL,
    "PublicationChannel" TEXT NOT NULL,
    CONSTRAINT "FK_PublishRecord_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ContentWorkflowTasks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ContentWorkflowTasks" PRIMARY KEY,
    "ContentWorkflowJobId" TEXT NOT NULL,
    "WorkflowTaskTemplateId" TEXT NULL,
    "Name" TEXT NOT NULL,
    "StageName" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "AssignedAgent" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "Kind" INTEGER NOT NULL,
    "ScopeType" INTEGER NOT NULL,
    "ScopeId" TEXT NULL,
    "Instructions" TEXT NOT NULL,
    "InputDataJson" TEXT NOT NULL,
    "OutputDataJson" TEXT NOT NULL,
    "StartedUtc" TEXT NULL,
    "CompletedUtc" TEXT NULL,
    "LastUpdatedUtc" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "RetryCount" INTEGER NOT NULL,
    "ParentTaskId" TEXT NULL,
    "InsertedAfterTaskId" TEXT NULL,
    "ResultArtifactId" TEXT NULL,
    "PatchPayload" TEXT NULL,
    "ExecutionAttemptCount" INTEGER NOT NULL,
    "SupersedesTaskId" TEXT NULL,
    CONSTRAINT "FK_ContentWorkflowTasks_ContentWorkflowJobs_ContentWorkflowJobId" FOREIGN KEY ("ContentWorkflowJobId") REFERENCES "ContentWorkflowJobs" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowRun" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowRun" PRIMARY KEY,
    "WorkflowId" TEXT NOT NULL,
    "StartedAt" TEXT NOT NULL,
    "CompletedAt" TEXT NULL,
    "IsSuccessful" INTEGER NOT NULL,
    "ErrorMessage" TEXT NULL,
    "ArticleId" TEXT NULL,
    CONSTRAINT "FK_WorkflowRun_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id"),
    CONSTRAINT "FK_WorkflowRun_Workflow_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "Workflow" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowDefinitions" PRIMARY KEY,
    "WorkflowDefinitionFamilyId" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "WorkflowType" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "PublishedUtc" TEXT NOT NULL,
    "PublishedBy" TEXT NOT NULL,
    CONSTRAINT "FK_WorkflowDefinitions_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId" FOREIGN KEY ("WorkflowDefinitionFamilyId") REFERENCES "WorkflowDefinitionFamilies" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowTemplateTask" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowTemplateTask" PRIMARY KEY,
    "WorkflowTemplateId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "StageName" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "AssignedAgent" TEXT NOT NULL,
    "DefaultInstructions" TEXT NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    CONSTRAINT "FK_WorkflowTemplateTask_WorkflowTemplates_WorkflowTemplateId" FOREIGN KEY ("WorkflowTemplateId") REFERENCES "WorkflowTemplates" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ArticleEditRequests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleEditRequests" PRIMARY KEY,
    "ArticleId" TEXT NOT NULL,
    "ThreadId" TEXT NOT NULL,
    "UserMessage" TEXT NOT NULL,
    "Intent" TEXT NOT NULL,
    "Scope" TEXT NOT NULL,
    "TargetSectionId" TEXT NOT NULL,
    "TargetLabel" TEXT NOT NULL,
    "SelectedText" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "RoutedAgent" TEXT NOT NULL,
    "QaStatus" TEXT NOT NULL,
    "QaSummary" TEXT NOT NULL,
    "DiffSummary" TEXT NOT NULL,
    "ProposedTitle" TEXT NOT NULL,
    "ProposedSummary" TEXT NOT NULL,
    "ProposedContent" TEXT NOT NULL,
    "ProposedMetaDescription" TEXT NOT NULL,
    "ProposedCallToAction" TEXT NOT NULL,
    "ProposalJson" TEXT NOT NULL,
    "LockedFields" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ReviewedAtUtc" TEXT NULL,
    "AppliedAtUtc" TEXT NULL,
    CONSTRAINT "FK_ArticleEditRequests_ArticleChatThreads_ThreadId" FOREIGN KEY ("ThreadId") REFERENCES "ArticleChatThreads" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowActionDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowActionDefinitions" PRIMARY KEY,
    "WorkflowDefinitionId" TEXT NOT NULL,
    "CapabilityKey" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "AssignedAgent" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    "Instructions" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    CONSTRAINT "FK_WorkflowActionDefinitions_WorkflowDefinitions_WorkflowDefinitionId" FOREIGN KEY ("WorkflowDefinitionId") REFERENCES "WorkflowDefinitions" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ArticleChatMessages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleChatMessages" PRIMARY KEY,
    "ThreadId" TEXT NOT NULL,
    "RequestId" TEXT NULL,
    "Role" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_ArticleChatMessages_ArticleChatThreads_ThreadId" FOREIGN KEY ("ThreadId") REFERENCES "ArticleChatThreads" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ArticleChatMessages_ArticleEditRequests_RequestId" FOREIGN KEY ("RequestId") REFERENCES "ArticleEditRequests" ("Id")
);


CREATE TABLE "ArticleEditPatches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ArticleEditPatches" PRIMARY KEY,
    "RequestId" TEXT NOT NULL,
    "TargetType" TEXT NOT NULL,
    "TargetId" TEXT NOT NULL,
    "Operation" TEXT NOT NULL,
    "BeforeContent" TEXT NOT NULL,
    "ProposedContent" TEXT NOT NULL,
    "Rationale" TEXT NOT NULL,
    "Warnings" TEXT NOT NULL,
    "FieldLocksRespected" INTEGER NOT NULL,
    "SortOrder" INTEGER NOT NULL,
    CONSTRAINT "FK_ArticleEditPatches_ArticleEditRequests_RequestId" FOREIGN KEY ("RequestId") REFERENCES "ArticleEditRequests" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowStepDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowStepDefinitions" PRIMARY KEY,
    "WorkflowActionDefinitionId" TEXT NOT NULL,
    "CapabilityKey" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    "Instructions" TEXT NOT NULL,
    "ExpectedOutput" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    CONSTRAINT "FK_WorkflowStepDefinitions_WorkflowActionDefinitions_WorkflowActionDefinitionId" FOREIGN KEY ("WorkflowActionDefinitionId") REFERENCES "WorkflowActionDefinitions" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IdeaRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_IdeaRecords" PRIMARY KEY,
    "SourceWorkflowRunId" TEXT NULL,
    "SourceWorkflowDefinitionId" TEXT NULL,
    "WorkflowVersion" INTEGER NOT NULL,
    "SiteId" TEXT NULL,
    "IdeaTitle" TEXT NOT NULL,
    "ReaderProblem" TEXT NOT NULL,
    "AudienceType" TEXT NOT NULL,
    "SearchIntent" TEXT NOT NULL,
    "EmotionalTrigger" TEXT NOT NULL,
    "UniquenessAngle" TEXT NOT NULL,
    "MonetizationFit" TEXT NOT NULL,
    "SeoPotential" TEXT NOT NULL,
    "Difficulty" TEXT NOT NULL,
    "PriorityScore" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    "ApprovedUtc" TEXT NULL,
    "ApprovedBy" TEXT NULL,
    "RejectedUtc" TEXT NULL,
    "RejectedBy" TEXT NULL,
    "IdeaSnapshotJson" TEXT NOT NULL,
    "CanonicalTopic" TEXT NOT NULL,
    "Angle" TEXT NOT NULL,
    "Intent" TEXT NOT NULL,
    "PainPoint" TEXT NOT NULL,
    CONSTRAINT "FK_IdeaRecords_WorkflowDefinitions_SourceWorkflowDefinitionId" FOREIGN KEY ("SourceWorkflowDefinitionId") REFERENCES "WorkflowDefinitions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IdeaRecords_WorkflowDefinitionRuns_SourceWorkflowRunId" FOREIGN KEY ("SourceWorkflowRunId") REFERENCES "WorkflowDefinitionRuns" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "WorkflowDefinitionRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowDefinitionRuns" PRIMARY KEY,
    "WorkflowDefinitionFamilyId" TEXT NOT NULL,
    "WorkflowDefinitionId" TEXT NOT NULL,
    "WorkflowType" INTEGER NOT NULL,
    "Version" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "StartedUtc" TEXT NOT NULL,
    "CompletedUtc" TEXT NULL,
    "TriggeredBy" TEXT NOT NULL,
    "ErrorMessage" TEXT NULL,
    "InputSnapshotJson" TEXT NOT NULL,
    "IdeaRecordId" TEXT NULL,
    CONSTRAINT "FK_WorkflowDefinitionRuns_IdeaRecords_IdeaRecordId" FOREIGN KEY ("IdeaRecordId") REFERENCES "IdeaRecords" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkflowDefinitionRuns_WorkflowDefinitionFamilies_WorkflowDefinitionFamilyId" FOREIGN KEY ("WorkflowDefinitionFamilyId") REFERENCES "WorkflowDefinitionFamilies" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkflowDefinitionRuns_WorkflowDefinitions_WorkflowDefinitionId" FOREIGN KEY ("WorkflowDefinitionId") REFERENCES "WorkflowDefinitions" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "WorkflowActionRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowActionRuns" PRIMARY KEY,
    "WorkflowDefinitionRunId" TEXT NOT NULL,
    "WorkflowActionDefinitionId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "RetryCount" INTEGER NOT NULL,
    "MaxRetry" INTEGER NOT NULL,
    "StartedUtc" TEXT NULL,
    "CompletedUtc" TEXT NULL,
    "LastFailureUtc" TEXT NULL,
    "ErrorMessage" TEXT NULL,
    "InputSnapshotJson" TEXT NULL,
    "OutputSnapshotJson" TEXT NULL,
    CONSTRAINT "FK_WorkflowActionRuns_WorkflowActionDefinitions_WorkflowActionDefinitionId" FOREIGN KEY ("WorkflowActionDefinitionId") REFERENCES "WorkflowActionDefinitions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkflowActionRuns_WorkflowDefinitionRuns_WorkflowDefinitionRunId" FOREIGN KEY ("WorkflowDefinitionRunId") REFERENCES "WorkflowDefinitionRuns" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WorkflowStepRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowStepRuns" PRIMARY KEY,
    "WorkflowActionRunId" TEXT NOT NULL,
    "WorkflowStepDefinitionId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "StartedUtc" TEXT NULL,
    "CompletedUtc" TEXT NULL,
    "ErrorMessage" TEXT NULL,
    "InputSnapshotJson" TEXT NULL,
    "OutputSnapshotJson" TEXT NULL,
    CONSTRAINT "FK_WorkflowStepRuns_WorkflowActionRuns_WorkflowActionRunId" FOREIGN KEY ("WorkflowActionRunId") REFERENCES "WorkflowActionRuns" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_WorkflowStepRuns_WorkflowStepDefinitions_WorkflowStepDefinitionId" FOREIGN KEY ("WorkflowStepDefinitionId") REFERENCES "WorkflowStepDefinitions" ("Id") ON DELETE RESTRICT
);


CREATE INDEX "IX_ArticleChatMessages_RequestId" ON "ArticleChatMessages" ("RequestId");


CREATE INDEX "IX_ArticleChatMessages_ThreadId_CreatedAtUtc" ON "ArticleChatMessages" ("ThreadId", "CreatedAtUtc");


CREATE INDEX "IX_ArticleChatThreads_ArticleId" ON "ArticleChatThreads" ("ArticleId");


CREATE INDEX "IX_ArticleEditPatches_RequestId_SortOrder" ON "ArticleEditPatches" ("RequestId", "SortOrder");


CREATE INDEX "IX_ArticleEditRequests_ArticleId_CreatedAtUtc" ON "ArticleEditRequests" ("ArticleId", "CreatedAtUtc");


CREATE INDEX "IX_ArticleEditRequests_ThreadId" ON "ArticleEditRequests" ("ThreadId");


CREATE UNIQUE INDEX "IX_ArticleRevisions_ArticleId_RevisionNumber" ON "ArticleRevisions" ("ArticleId", "RevisionNumber");


CREATE INDEX "IX_ArticleVersions_ArticleId" ON "ArticleVersions" ("ArticleId");


CREATE INDEX "IX_ContentIdeas_Status" ON "ContentIdeas" ("Status");


CREATE UNIQUE INDEX "IX_ContentWorkflowJobs_ContentIdeaId" ON "ContentWorkflowJobs" ("ContentIdeaId");


CREATE INDEX "IX_ContentWorkflowJobs_Status" ON "ContentWorkflowJobs" ("Status");


CREATE INDEX "IX_ContentWorkflowTasks_ContentWorkflowJobId_DisplayOrder" ON "ContentWorkflowTasks" ("ContentWorkflowJobId", "DisplayOrder");


CREATE INDEX "IX_ContentWorkflowTasks_Status" ON "ContentWorkflowTasks" ("Status");


CREATE INDEX "IX_IdeaRecords_CreatedUtc" ON "IdeaRecords" ("CreatedUtc");


CREATE INDEX "IX_IdeaRecords_SourceWorkflowDefinitionId" ON "IdeaRecords" ("SourceWorkflowDefinitionId");


CREATE INDEX "IX_IdeaRecords_SourceWorkflowRunId" ON "IdeaRecords" ("SourceWorkflowRunId");


CREATE INDEX "IX_IdeaRecords_Status_PriorityScore" ON "IdeaRecords" ("Status", "PriorityScore");


CREATE INDEX "IX_PublishRecord_ArticleId" ON "PublishRecord" ("ArticleId");


CREATE UNIQUE INDEX "IX_WorkflowActionDefinitions_WorkflowDefinitionId_Order" ON "WorkflowActionDefinitions" ("WorkflowDefinitionId", "Order");


CREATE INDEX "IX_WorkflowActionRuns_WorkflowActionDefinitionId" ON "WorkflowActionRuns" ("WorkflowActionDefinitionId");


CREATE INDEX "IX_WorkflowActionRuns_WorkflowDefinitionRunId_Order" ON "WorkflowActionRuns" ("WorkflowDefinitionRunId", "Order");


CREATE UNIQUE INDEX "IX_WorkflowActionRuns_WorkflowDefinitionRunId_WorkflowActionDefinitionId" ON "WorkflowActionRuns" ("WorkflowDefinitionRunId", "WorkflowActionDefinitionId");


CREATE UNIQUE INDEX "IX_WorkflowDefinitionFamilies_Name_WorkflowType" ON "WorkflowDefinitionFamilies" ("Name", "WorkflowType");


CREATE INDEX "IX_WorkflowDefinitionMutations_WorkflowDefinitionId_CreatedUtc" ON "WorkflowDefinitionMutations" ("WorkflowDefinitionId", "CreatedUtc");


CREATE INDEX "IX_WorkflowDefinitionRuns_IdeaRecordId" ON "WorkflowDefinitionRuns" ("IdeaRecordId");


CREATE INDEX "IX_WorkflowDefinitionRuns_WorkflowDefinitionFamilyId" ON "WorkflowDefinitionRuns" ("WorkflowDefinitionFamilyId");


CREATE INDEX "IX_WorkflowDefinitionRuns_WorkflowDefinitionId_StartedUtc" ON "WorkflowDefinitionRuns" ("WorkflowDefinitionId", "StartedUtc");


CREATE INDEX "IX_WorkflowDefinitionRuns_WorkflowType_Status" ON "WorkflowDefinitionRuns" ("WorkflowType", "Status");


CREATE INDEX "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_IsActive" ON "WorkflowDefinitions" ("WorkflowDefinitionFamilyId", "IsActive");


CREATE UNIQUE INDEX "IX_WorkflowDefinitions_WorkflowDefinitionFamilyId_Version" ON "WorkflowDefinitions" ("WorkflowDefinitionFamilyId", "Version");


CREATE INDEX "IX_WorkflowMutationLogs_ContentWorkflowJobId_CreatedUtc" ON "WorkflowMutationLogs" ("ContentWorkflowJobId", "CreatedUtc");


CREATE INDEX "IX_WorkflowRun_ArticleId" ON "WorkflowRun" ("ArticleId");


CREATE INDEX "IX_WorkflowRun_WorkflowId" ON "WorkflowRun" ("WorkflowId");


CREATE UNIQUE INDEX "IX_WorkflowStepDefinitions_WorkflowActionDefinitionId_Order" ON "WorkflowStepDefinitions" ("WorkflowActionDefinitionId", "Order");


CREATE INDEX "IX_WorkflowStepRuns_WorkflowActionRunId_Order" ON "WorkflowStepRuns" ("WorkflowActionRunId", "Order");


CREATE UNIQUE INDEX "IX_WorkflowStepRuns_WorkflowActionRunId_WorkflowStepDefinitionId" ON "WorkflowStepRuns" ("WorkflowActionRunId", "WorkflowStepDefinitionId");


CREATE INDEX "IX_WorkflowStepRuns_WorkflowStepDefinitionId" ON "WorkflowStepRuns" ("WorkflowStepDefinitionId");


CREATE INDEX "IX_WorkflowTaskTemplates_WorkflowTemplateId_DisplayOrder" ON "WorkflowTaskTemplates" ("WorkflowTemplateId", "DisplayOrder");


CREATE UNIQUE INDEX "IX_WorkflowTemplates_Name" ON "WorkflowTemplates" ("Name");


CREATE INDEX "IX_WorkflowTemplateTask_WorkflowTemplateId" ON "WorkflowTemplateTask" ("WorkflowTemplateId");
