using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTablesInSOSFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirstAidDetail_Snake_SnakeId",
                table: "FirstAidDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_Incident_Snake_SnakeId",
                table: "Incident");

            migrationBuilder.RenameIndex(
                name: "IX_SnakeHotspot_snake_id",
                table: "SnakeHotspot",
                newName: "IX_SnakeHotspot_SnakeId");

            migrationBuilder.AddColumn<string>(
                name: "observation_type",
                table: "SnakeHotspot",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "toxin_group",
                table: "SnakeHotspot",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "distribution_note",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "toxin_group",
                table: "Snake",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "available_updated_at",
                table: "RescuerProfile",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_available",
                table: "RescuerProfile",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "current_ai_inference_id",
                table: "Incident",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "snake_id",
                table: "FirstAidDetail",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "toxin_group",
                table: "FirstAidDetail",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AiInference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_media_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    model_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    model_version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    top_k = table.Column<int>(type: "int", nullable: false),
                    selected_snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    selected_confidence = table.Column<double>(type: "float", nullable: true),
                    selected_toxin_group = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    decision_rule = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInference_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_AiInference_IncidentMedia_IncidentMediaId",
                        column: x => x.incident_media_id,
                        principalTable: "IncidentMedia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiInference_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiInference_Snake_SelectedSnakeId",
                        column: x => x.selected_snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AiInferenceCandidate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ai_inference_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rank = table.Column<int>(type: "int", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    confidence = table.Column<double>(type: "float", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInferenceCandidate_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_AiInferenceCandidate_AiInference_AiInferenceId",
                        column: x => x.ai_inference_id,
                        principalTable: "AiInference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiInferenceCandidate_Snake_SnakeId",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnakeHotspot_ExpiresAt",
                table: "SnakeHotspot",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_current_ai_inference_id",
                table: "Incident",
                column: "current_ai_inference_id");

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidDetail_ToxinGroup_Lang_Order_Snake",
                table: "FirstAidDetail",
                columns: new[] { "toxin_group", "language_code", "step_order", "snake_id" },
                unique: true,
                filter: "[snake_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiInference_incident_media_id",
                table: "AiInference",
                column: "incident_media_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiInference_IncidentId",
                table: "AiInference",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiInference_selected_snake_id",
                table: "AiInference",
                column: "selected_snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceCandidate_Inference_Rank",
                table: "AiInferenceCandidate",
                columns: new[] { "ai_inference_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceCandidate_snake_id",
                table: "AiInferenceCandidate",
                column: "snake_id");

            migrationBuilder.AddForeignKey(
                name: "FK_FirstAidDetail_Snake_SnakeId",
                table: "FirstAidDetail",
                column: "snake_id",
                principalTable: "Snake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incident_AiInference_CurrentAiInferenceId",
                table: "Incident",
                column: "current_ai_inference_id",
                principalTable: "AiInference",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Incident_Snake_SnakeId",
                table: "Incident",
                column: "snake_id",
                principalTable: "Snake",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirstAidDetail_Snake_SnakeId",
                table: "FirstAidDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_Incident_AiInference_CurrentAiInferenceId",
                table: "Incident");

            migrationBuilder.DropForeignKey(
                name: "FK_Incident_Snake_SnakeId",
                table: "Incident");

            migrationBuilder.DropTable(
                name: "AiInferenceCandidate");

            migrationBuilder.DropTable(
                name: "AiInference");

            migrationBuilder.DropIndex(
                name: "IX_SnakeHotspot_ExpiresAt",
                table: "SnakeHotspot");

            migrationBuilder.DropIndex(
                name: "IX_Incident_current_ai_inference_id",
                table: "Incident");

            migrationBuilder.DropIndex(
                name: "IX_FirstAidDetail_ToxinGroup_Lang_Order_Snake",
                table: "FirstAidDetail");

            migrationBuilder.DropColumn(
                name: "observation_type",
                table: "SnakeHotspot");

            migrationBuilder.DropColumn(
                name: "toxin_group",
                table: "SnakeHotspot");

            migrationBuilder.DropColumn(
                name: "distribution_note",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "toxin_group",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "available_updated_at",
                table: "RescuerProfile");

            migrationBuilder.DropColumn(
                name: "is_available",
                table: "RescuerProfile");

            migrationBuilder.DropColumn(
                name: "current_ai_inference_id",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "toxin_group",
                table: "FirstAidDetail");

            migrationBuilder.RenameIndex(
                name: "IX_SnakeHotspot_SnakeId",
                table: "SnakeHotspot",
                newName: "IX_SnakeHotspot_snake_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "snake_id",
                table: "FirstAidDetail",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FirstAidDetail_Snake_SnakeId",
                table: "FirstAidDetail",
                column: "snake_id",
                principalTable: "Snake",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Incident_Snake_SnakeId",
                table: "Incident",
                column: "snake_id",
                principalTable: "Snake",
                principalColumn: "id");
        }
    }
}
