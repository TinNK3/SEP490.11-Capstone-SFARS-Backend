using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentCurrentAiReviewNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Incident_current_ai_review_id",
                table: "Incident",
                column: "current_ai_review_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Incident_AiInferenceReview_CurrentAiReviewId",
                table: "Incident",
                column: "current_ai_review_id",
                principalTable: "AiInferenceReview",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incident_AiInferenceReview_CurrentAiReviewId",
                table: "Incident");

            migrationBuilder.DropIndex(
                name: "IX_Incident_current_ai_review_id",
                table: "Incident");
        }
    }
}
