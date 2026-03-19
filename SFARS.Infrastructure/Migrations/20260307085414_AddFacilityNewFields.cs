using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "operating_hours",
                table: "MedicalFacility",
                newName: "province");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "close_hours",
                table: "MedicalFacility",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "MedicalFacility",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "emergency_available",
                table: "MedicalFacility",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "MedicalFacility",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "open_hours",
                table: "MedicalFacility",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "MedicalFacility",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "close_hours",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "email",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "emergency_available",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "open_hours",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "website",
                table: "MedicalFacility");

            migrationBuilder.RenameColumn(
                name: "province",
                table: "MedicalFacility",
                newName: "operating_hours");
        }
    }
}
