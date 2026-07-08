using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialWorker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssetSha256 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "MediaAssets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "MediaAssets");
        }
    }
}
