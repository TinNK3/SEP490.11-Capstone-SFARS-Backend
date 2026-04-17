using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsConfirmedWoundToReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmedWoundSnakeBite",
                table: "AiInferenceReview",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsConfirmedWoundSnakeBite",
                table: "AiInferenceReview");
        }
    }
}
