using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForIncidentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL to drop tables if they exist from previous messy migrations
            // This avoids EF Core's KeyNotFoundException during migration SQL generation
            migrationBuilder.Sql("IF OBJECT_ID('dbo.IncidentStatusHistory', 'U') IS NOT NULL DROP TABLE dbo.IncidentStatusHistory;");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.NotificationLog', 'U') IS NOT NULL DROP TABLE dbo.NotificationLog;");

            // ==================== IncidentStatusHistory ====================
            migrationBuilder.CreateTable(
                name: "IncidentStatusHistory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status_from = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    status_to = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    change_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentStatusHistory", x => x.id);
                    table.ForeignKey(
                        name: "FK_IncidentStatusHistory_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentStatusHistory_IncidentId",
                table: "IncidentStatusHistory",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentStatusHistory_CreatedAt",
                table: "IncidentStatusHistory",
                column: "created_at");

            // ==================== NotificationLog ====================
            migrationBuilder.CreateTable(
                name: "NotificationLog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLog", x => x.id);
                    table.ForeignKey(
                        name: "FK_NotificationLog_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_UserId",
                table: "NotificationLog",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_SentAt",
                table: "NotificationLog",
                column: "sent_at");

            // ==================== Create SQL SEQUENCE ====================
            // Removed duplicate CREATE SEQUENCE dbo.IncidentCodeSeq because it's already in 20260206041000_AddIncidentCodeSequence
            // migrationBuilder.Sql("CREATE SEQUENCE dbo.IncidentCodeSeq START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot reverse the recreation easily, but we'll try to drop them since they were recreated.
            migrationBuilder.DropTable(name: "NotificationLog");
            migrationBuilder.DropTable(name: "IncidentStatusHistory");
        }
    }
}
