using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetPlatformAndCanonicalDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalDraftId",
                table: "Drafts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetPlatform",
                table: "Drafts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_CanonicalDraftId",
                table: "Drafts",
                column: "CanonicalDraftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Drafts_Drafts_CanonicalDraftId",
                table: "Drafts",
                column: "CanonicalDraftId",
                principalTable: "Drafts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drafts_Drafts_CanonicalDraftId",
                table: "Drafts");

            migrationBuilder.DropIndex(
                name: "IX_Drafts_CanonicalDraftId",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "CanonicalDraftId",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "TargetPlatform",
                table: "Drafts");
        }
    }
}
