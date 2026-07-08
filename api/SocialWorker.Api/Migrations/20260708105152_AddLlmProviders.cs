using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreferredProviderId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LlmProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreferredProviderId",
                table: "Users",
                column: "PreferredProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmProviders_Name",
                table: "LlmProviders",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_LlmProviders_PreferredProviderId",
                table: "Users",
                column: "PreferredProviderId",
                principalTable: "LlmProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_LlmProviders_PreferredProviderId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "LlmProviders");

            migrationBuilder.DropIndex(
                name: "IX_Users_PreferredProviderId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredProviderId",
                table: "Users");
        }
    }
}
