using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminReviewLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_ActiveReview",
                table: "AiInferenceReview");



            migrationBuilder.AddColumn<bool>(
                name: "human_confirmed_snake_bite",
                table: "Incident",
                type: "bit",
                nullable: true);



            migrationBuilder.AddColumn<string>(
                name: "admin_comment",
                table: "AiInferenceReview",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_review_status",
                table: "AiInferenceReview",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "admin_reviewer_id",
                table: "AiInferenceReview",
                type: "uniqueidentifier",
                nullable: true);



            migrationBuilder.AddForeignKey(
                name: "FK_AiInferenceReview_User_AdminReviewerId",
                table: "AiInferenceReview",
                column: "admin_reviewer_id",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInferenceReview_User_AdminReviewerId",
                table: "AiInferenceReview");



            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_ActiveReview",
                table: "AiInferenceReview");

            migrationBuilder.DropIndex(
                name: "IX_AiInferenceReview_admin_reviewer_id",
                table: "AiInferenceReview");



            migrationBuilder.DropColumn(
                name: "human_confirmed_snake_bite",
                table: "Incident");



            migrationBuilder.DropColumn(
                name: "admin_comment",
                table: "AiInferenceReview");

            migrationBuilder.DropColumn(
                name: "admin_review_status",
                table: "AiInferenceReview");

            migrationBuilder.DropColumn(
                name: "admin_reviewer_id",
                table: "AiInferenceReview");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceReview_ActiveReview",
                table: "AiInferenceReview",
                column: "incident_id",
                unique: true,
                filter: "[review_status] IN ('Pending', 'Deferred')");
        }
    }
}
