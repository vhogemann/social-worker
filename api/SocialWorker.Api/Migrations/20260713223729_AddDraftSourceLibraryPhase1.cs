using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftSourceLibraryPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptPath",
                table: "Sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptStatus",
                table: "Sources",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "YoutubeVideoId",
                table: "Sources",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DraftSources",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftSources", x => new { x.DraftId, x.SourceId });
                    table.ForeignKey(
                        name: "FK_DraftSources_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftSources_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "DraftSources" ("DraftId", "SourceId", "LinkedAt")
                SELECT "DraftId", "Id", COALESCE("AddedAt", NOW())
                FROM "Sources"
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Sources"
                ADD COLUMN "ContentTsv" tsvector
                GENERATED ALWAYS AS (to_tsvector('english', coalesce("Title", '') || ' ' || coalesce("Content", ''))) STORED
                """);

            migrationBuilder.Sql(
                "CREATE INDEX \"idx_sources_content_fts\" ON \"Sources\" USING GIN (\"ContentTsv\")");

            migrationBuilder.DropForeignKey(
                name: "FK_Sources_Drafts_DraftId",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_DraftId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DraftId",
                table: "Sources");

            migrationBuilder.CreateIndex(
                name: "IX_DraftSources_DraftId_SourceId",
                table: "DraftSources",
                columns: new[] { "DraftId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftSources_SourceId",
                table: "DraftSources",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"idx_sources_content_fts\"");

            migrationBuilder.Sql("ALTER TABLE \"Sources\" DROP COLUMN IF EXISTS \"ContentTsv\"");

            migrationBuilder.DropTable(
                name: "DraftSources");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "TranscriptPath",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "TranscriptStatus",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "YoutubeVideoId",
                table: "Sources");

            migrationBuilder.AddColumn<Guid>(
                name: "DraftId",
                table: "Sources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Sources_DraftId",
                table: "Sources",
                column: "DraftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sources_Drafts_DraftId",
                table: "Sources",
                column: "DraftId",
                principalTable: "Drafts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
