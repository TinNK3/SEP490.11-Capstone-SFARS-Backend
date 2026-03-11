using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAiReviewIncidentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "AiInferenceReview",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_incident_id",
                table: "AiInferenceReview",
                column: "incident_id");

            migrationBuilder.AddForeignKey(
                name: "FK_AiInferenceReview_Incident_IncidentId",
                table: "AiInferenceReview",
                column: "incident_id",
                principalTable: "Incident",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInferenceReview_Incident_IncidentId",
                table: "AiInferenceReview");

            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_incident_id",
                table: "AiInferenceReview");

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "AiInferenceReview");
        }
    }
}
