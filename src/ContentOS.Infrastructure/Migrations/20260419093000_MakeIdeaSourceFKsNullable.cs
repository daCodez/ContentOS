using Microsoft.EntityFrameworkCore.Migrations;

namespace ContentOS.Infrastructure.Migrations;

/// <summary>
/// Make SourceWorkflowRunId and SourceWorkflowDefinitionId nullable on IdeaRecords
/// so that manually-generated ideas don't require a foreign key reference.
/// </summary>
public partial class MakeIdeaSourceFKsNullable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "SourceWorkflowDefinitionId",
            table: "IdeaRecords",
            type: "TEXT",
            nullable: true,
            oldType: "TEXT",
            oldNullable: false);

        migrationBuilder.AlterColumn<Guid>(
            name: "SourceWorkflowRunId",
            table: "IdeaRecords",
            type: "TEXT",
            nullable: true,
            oldType: "TEXT",
            oldNullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "SourceWorkflowRunId",
            table: "IdeaRecords",
            type: "TEXT",
            nullable: false,
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SourceWorkflowDefinitionId",
            table: "IdeaRecords",
            type: "TEXT",
            nullable: false,
            oldType: "TEXT",
            oldNullable: true);
    }
}