using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiReviewActiveConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_incident_id",
                table: "AiInferenceReview");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_ActiveReview",
                table: "AiInferenceReview",
                column: "incident_id",
                unique: true,
                filter: "[review_status] IN ('Pending', 'Deferred')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_ActiveReview",
                table: "AiInferenceReview");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_incident_id",
                table: "AiInferenceReview",
                column: "incident_id");
        }
    }
}
