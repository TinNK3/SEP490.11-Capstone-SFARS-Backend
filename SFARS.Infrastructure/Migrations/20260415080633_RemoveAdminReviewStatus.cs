using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "admin_review_status",
                table: "AiInferenceReview");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_review_status",
                table: "AiInferenceReview",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
