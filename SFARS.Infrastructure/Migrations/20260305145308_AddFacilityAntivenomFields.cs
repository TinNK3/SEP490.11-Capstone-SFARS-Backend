using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityAntivenomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "antivenom_updated_at",
                table: "MedicalFacility",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_antivenom",
                table: "MedicalFacility",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "antivenom_updated_at",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "has_antivenom",
                table: "MedicalFacility");
        }
    }
}
