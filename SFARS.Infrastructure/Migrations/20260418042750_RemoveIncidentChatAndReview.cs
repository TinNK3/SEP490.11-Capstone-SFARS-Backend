using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIncidentChatAndReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentChatMessageMedias");

            migrationBuilder.DropTable(
                name: "Review");

            migrationBuilder.DropTable(
                name: "IncidentChatMessages");

            migrationBuilder.DropTable(
                name: "IncidentChats");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentChats_Incident_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    mission_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rating = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Review_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_Review_RescueMission_MissionId",
                        column: x => x.mission_id,
                        principalTable: "RescueMission",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Review_User_ReviewerId",
                        column: x => x.reviewer_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Review_User_TargetId",
                        column: x => x.target_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiInferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentChatMessages_AiInference_AiInferenceId",
                        column: x => x.AiInferenceId,
                        principalTable: "AiInference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IncidentChatMessages_IncidentChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "IncidentChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentChatMessages_User_SenderId",
                        column: x => x.SenderId,
                        principalTable: "User",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "IncidentChatMessageMedias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MediaUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentChatMessageMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentChatMessageMedias_IncidentChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "IncidentChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentChatMessageMedias_IncidentMedia_IncidentMediaId",
                        column: x => x.IncidentMediaId,
                        principalTable: "IncidentMedia",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChatMessageMedias_IncidentMediaId",
                table: "IncidentChatMessageMedias",
                column: "IncidentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChatMessageMedias_MessageId",
                table: "IncidentChatMessageMedias",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChatMessages_AiInferenceId",
                table: "IncidentChatMessages",
                column: "AiInferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChatMessages_ChatId_CreatedAt",
                table: "IncidentChatMessages",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChatMessages_SenderId",
                table: "IncidentChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentChats_IncidentId",
                table: "IncidentChats",
                column: "IncidentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Review_mission_id",
                table: "Review",
                column: "mission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Review_reviewer_id",
                table: "Review",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_Review_target_id",
                table: "Review",
                column: "target_id");
        }
    }
}
