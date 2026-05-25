using Microsoft.EntityFrameworkCore.Migrations;

namespace ContentOS.Infrastructure.Migrations;

[Migration("20260414233500_AddTopicDiversityFields")]
public partial class AddTopicDiversityFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("TopicType", "ContentIdeas", nullable: false, defaultValue: "Problem");
        migrationBuilder.AddColumn<string>("SpecificityTag", "ContentIdeas", nullable: false, defaultValue: "general");
        migrationBuilder.AddColumn<string>("IntentMatchScore", "ContentIdeas", nullable: false, defaultValue: "0");
        migrationBuilder.AddColumn<string>("UniquenessScore", "ContentIdeas", nullable: false, defaultValue: "0");
        migrationBuilder.AddColumn<string>("ClickPotentialScore", "ContentIdeas", nullable: false, defaultValue: "0");
        migrationBuilder.AddColumn<string>("MonetizationPotentialScore", "ContentIdeas", nullable: false, defaultValue: "0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("TopicType", "ContentIdeas");
        migrationBuilder.DropColumn("SpecificityTag", "ContentIdeas");
        migrationBuilder.DropColumn("IntentMatchScore", "ContentIdeas");
        migrationBuilder.DropColumn("UniquenessScore", "ContentIdeas");
        migrationBuilder.DropColumn("ClickPotentialScore", "ContentIdeas");
        migrationBuilder.DropColumn("MonetizationPotentialScore", "ContentIdeas");
    }
}
