using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRescuerAiReviewFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_ai_review_id",
                table: "Incident",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_ai_review_status",
                table: "Incident",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "human_reviewed_snake_id",
                table: "Incident",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "human_reviewed_toxin_group",
                table: "Incident",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiInferenceReview",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ai_inference_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    review_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    corrected_snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    corrected_toxin_group = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    unable_to_assess_reason_choice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInferenceReview_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_AiInferenceReview_AiInference_AiInferenceId",
                        column: x => x.ai_inference_id,
                        principalTable: "AiInference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiInferenceReview_Snake_CorrectedSnakeId",
                        column: x => x.corrected_snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiInferenceReview_User_ReviewerId",
                        column: x => x.reviewer_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiReviewAuditLog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    review_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rescuer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    old_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    new_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReviewAuditLog_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_AiReviewAuditLog_AiInferenceReview_ReviewId",
                        column: x => x.review_id,
                        principalTable: "AiInferenceReview",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_AiReviewAuditLog_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiReviewAuditLog_User_RescuerId",
                        column: x => x.rescuer_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_ai_inference_id",
                table: "AiInferenceReview",
                column: "ai_inference_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_corrected_snake_id",
                table: "AiInferenceReview",
                column: "corrected_snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_reviewer_id",
                table: "AiInferenceReview",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiReviewAuditLog_incident_id",
                table: "AiReviewAuditLog",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiReviewAuditLog_rescuer_id",
                table: "AiReviewAuditLog",
                column: "rescuer_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiReviewAuditLog_review_id",
                table: "AiReviewAuditLog",
                column: "review_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiReviewAuditLog");

            migrationBuilder.DropTable(
                name: "AiInferenceReview");

            migrationBuilder.DropColumn(
                name: "current_ai_review_id",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "current_ai_review_status",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "human_reviewed_snake_id",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "human_reviewed_toxin_group",
                table: "Incident");
        }
    }
}
