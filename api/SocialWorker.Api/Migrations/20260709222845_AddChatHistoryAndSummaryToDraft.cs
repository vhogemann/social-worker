using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatHistoryAndSummaryToDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatHistory",
                table: "Drafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatSummary",
                table: "Drafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSummarizedMessageCount",
                table: "Drafts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatHistory",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "ChatSummary",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "LastSummarizedMessageCount",
                table: "Drafts");
        }
    }
}
