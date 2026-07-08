using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformThreadsAndRemoveDraftStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformThreads_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformThreads_DraftId_Platform",
                table: "PlatformThreads",
                columns: new[] { "DraftId", "Platform" },
                unique: true);

            // Data Migration: Set Status based on old Stage value
            migrationBuilder.Sql("UPDATE \"Drafts\" SET \"Status\" = 'Sourcing' WHERE \"Stage\" = 'Sourcing';");
            migrationBuilder.Sql("UPDATE \"Drafts\" SET \"Status\" = 'Formatting' WHERE \"Stage\" = 'Formatting';");

            // Data Migration: Insert default Bluesky platform thread for existing drafts mapping old stages
            migrationBuilder.Sql(@"
                INSERT INTO ""PlatformThreads"" (""Id"", ""DraftId"", ""Platform"", ""Stage"", ""Content"", ""CreatedAt"", ""UpdatedAt"")
                SELECT 
                    gen_random_uuid(), 
                    ""Id"", 
                    'Bluesky', 
                    CASE 
                        WHEN ""Stage"" = 'Ready' THEN 'Ready'
                        WHEN ""Stage"" = 'Sent' THEN 'Sent'
                        ELSE 'Draft'
                    END, 
                    ""Content"", 
                    NOW(), 
                    NOW()
                FROM ""Drafts"";
            ");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Drafts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformThreads");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Drafts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
