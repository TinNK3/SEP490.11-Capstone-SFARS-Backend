using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Reels",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenByAdmin",
                table: "Reels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                table: "ContentPost",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden_by_admin",
                table: "ContentPost",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "Reels");

            migrationBuilder.DropColumn(
                name: "IsHiddenByAdmin",
                table: "Reels");

            migrationBuilder.DropColumn(
                name: "admin_note",
                table: "ContentPost");

            migrationBuilder.DropColumn(
                name: "is_hidden_by_admin",
                table: "ContentPost");
        }
    }
}
