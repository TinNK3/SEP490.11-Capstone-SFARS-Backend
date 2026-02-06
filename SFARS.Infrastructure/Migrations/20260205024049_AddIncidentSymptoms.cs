using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentSymptoms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentSymptoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasBleeding = table.Column<bool>(type: "bit", nullable: false),
                    HasSwelling = table.Column<bool>(type: "bit", nullable: false),
                    HasNecrosis = table.Column<bool>(type: "bit", nullable: false),
                    HasBreathingDifficulty = table.Column<bool>(type: "bit", nullable: false),
                    HasPtosis = table.Column<bool>(type: "bit", nullable: false),
                    HasVomiting = table.Column<bool>(type: "bit", nullable: false),
                    HasPain = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentSymptoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentSymptoms_Incident_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentSymptoms_User_ReportedBy",
                        column: x => x.ReportedBy,
                        principalTable: "User",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentSymptoms_IncidentId",
                table: "IncidentSymptoms",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentSymptoms_ReportedAt",
                table: "IncidentSymptoms",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentSymptoms_ReportedBy",
                table: "IncidentSymptoms",
                column: "ReportedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentSymptoms");
        }
    }
}
