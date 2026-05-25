using Microsoft.EntityFrameworkCore.Migrations;

namespace ContentOS.Infrastructure.Migrations;

[Migration("20260417074000_AddArticleChatEditing")]
public partial class AddArticleChatEditing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("MetaDescription", "Articles", nullable: false, defaultValue: string.Empty);
        migrationBuilder.AddColumn<string>("CallToAction", "Articles", nullable: false, defaultValue: string.Empty);

        migrationBuilder.CreateTable(
            name: "ArticleChatThreads",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ArticleId = table.Column<Guid>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(nullable: false),
                Title = table.Column<string>(nullable: false)
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
            name: "ArticleEditRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ArticleId = table.Column<Guid>(nullable: false),
                ThreadId = table.Column<Guid>(nullable: false),
                UserMessage = table.Column<string>(nullable: false),
                Intent = table.Column<string>(nullable: false),
                Scope = table.Column<string>(nullable: false),
                TargetSectionId = table.Column<string>(nullable: false),
                TargetLabel = table.Column<string>(nullable: false),
                SelectedText = table.Column<string>(nullable: false),
                Status = table.Column<string>(nullable: false),
                RoutedAgent = table.Column<string>(nullable: false),
                QaStatus = table.Column<string>(nullable: false),
                QaSummary = table.Column<string>(nullable: false),
                DiffSummary = table.Column<string>(nullable: false),
                ProposedTitle = table.Column<string>(nullable: false),
                ProposedSummary = table.Column<string>(nullable: false),
                ProposedContent = table.Column<string>(nullable: false),
                ProposedMetaDescription = table.Column<string>(nullable: false),
                ProposedCallToAction = table.Column<string>(nullable: false),
                ProposalJson = table.Column<string>(nullable: false),
                LockedFields = table.Column<string>(nullable: false, defaultValue: string.Empty),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                ReviewedAtUtc = table.Column<DateTime>(nullable: true),
                AppliedAtUtc = table.Column<DateTime>(nullable: true)
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
            name: "ArticleChatMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ThreadId = table.Column<Guid>(nullable: false),
                RequestId = table.Column<Guid>(nullable: true),
                Role = table.Column<string>(nullable: false),
                Content = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
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
                Id = table.Column<Guid>(nullable: false),
                RequestId = table.Column<Guid>(nullable: false),
                TargetType = table.Column<string>(nullable: false),
                TargetId = table.Column<string>(nullable: false),
                Operation = table.Column<string>(nullable: false),
                BeforeContent = table.Column<string>(nullable: false),
                ProposedContent = table.Column<string>(nullable: false),
                Rationale = table.Column<string>(nullable: false),
                Warnings = table.Column<string>(nullable: false),
                FieldLocksRespected = table.Column<bool>(nullable: false),
                SortOrder = table.Column<int>(nullable: false)
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
            name: "ArticleRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ArticleId = table.Column<Guid>(nullable: false),
                RequestId = table.Column<Guid>(nullable: true),
                RevisionNumber = table.Column<int>(nullable: false),
                Title = table.Column<string>(nullable: false),
                Summary = table.Column<string>(nullable: false),
                Content = table.Column<string>(nullable: false),
                MetaDescription = table.Column<string>(nullable: false),
                CallToAction = table.Column<string>(nullable: false),
                DiffSummary = table.Column<string>(nullable: false),
                ChangedBy = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArticleRevisions", x => x.Id);
            });

        migrationBuilder.CreateIndex("IX_ArticleChatThreads_ArticleId", "ArticleChatThreads", "ArticleId");
        migrationBuilder.CreateIndex("IX_ArticleChatMessages_RequestId", "ArticleChatMessages", "RequestId");
        migrationBuilder.CreateIndex("IX_ArticleChatMessages_ThreadId_CreatedAtUtc", "ArticleChatMessages", new[] { "ThreadId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_ArticleEditRequests_ArticleId_CreatedAtUtc", "ArticleEditRequests", new[] { "ArticleId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_ArticleEditRequests_ThreadId", "ArticleEditRequests", "ThreadId");
        migrationBuilder.CreateIndex("IX_ArticleEditPatches_RequestId_SortOrder", "ArticleEditPatches", new[] { "RequestId", "SortOrder" });
        migrationBuilder.CreateIndex("IX_ArticleRevisions_ArticleId_RevisionNumber", "ArticleRevisions", new[] { "ArticleId", "RevisionNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ArticleChatMessages");
        migrationBuilder.DropTable("ArticleEditPatches");
        migrationBuilder.DropTable("ArticleRevisions");
        migrationBuilder.DropTable("ArticleEditRequests");
        migrationBuilder.DropTable("ArticleChatThreads");
        migrationBuilder.DropColumn("MetaDescription", "Articles");
        migrationBuilder.DropColumn("CallToAction", "Articles");
    }
}
