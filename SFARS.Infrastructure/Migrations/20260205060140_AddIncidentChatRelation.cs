using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentChatRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "IncidentSymptoms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "IncidentChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                name: "IncidentChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiInferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MediaUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentChatMessageMedias");

            migrationBuilder.DropTable(
                name: "IncidentChatMessages");

            migrationBuilder.DropTable(
                name: "IncidentChats");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "IncidentSymptoms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
