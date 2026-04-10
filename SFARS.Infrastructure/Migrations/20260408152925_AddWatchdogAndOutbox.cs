using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchdogAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "last_location_delta_meters",
                table: "User",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "last_checked_distance_meters",
                table: "RescueMission",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_check_at",
                table: "RescueMission",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dispatch_version",
                table: "Incident",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_processed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    processed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    retry_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage_Id", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RescueMission_NextCheckAt",
                table: "RescueMission",
                column: "next_check_at",
                filter: "[next_check_at] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_CreatedAt",
                table: "OutboxMessage",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_IsProcessed",
                table: "OutboxMessage",
                column: "is_processed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_RescueMission_NextCheckAt",
                table: "RescueMission");

            migrationBuilder.DropColumn(
                name: "last_location_delta_meters",
                table: "User");

            migrationBuilder.DropColumn(
                name: "last_checked_distance_meters",
                table: "RescueMission");

            migrationBuilder.DropColumn(
                name: "next_check_at",
                table: "RescueMission");

            migrationBuilder.DropColumn(
                name: "dispatch_version",
                table: "Incident");
        }
    }
}
