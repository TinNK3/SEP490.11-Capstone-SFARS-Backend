using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationFieldsForUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "location_accuracy_meters",
                table: "User",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "location_updated_at",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tracking_code",
                table: "Incident",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tracking_code_expires_at",
                table: "Incident",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_LocationUpdatedAt",
                table: "User",
                column: "location_updated_at",
                filter: "[location_updated_at] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_TrackingCode",
                table: "Incident",
                column: "tracking_code",
                unique: true,
                filter: "[tracking_code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_LocationUpdatedAt",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Incident_TrackingCode",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "location_accuracy_meters",
                table: "User");

            migrationBuilder.DropColumn(
                name: "location_updated_at",
                table: "User");

            migrationBuilder.DropColumn(
                name: "tracking_code",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "tracking_code_expires_at",
                table: "Incident");
        }
    }
}
