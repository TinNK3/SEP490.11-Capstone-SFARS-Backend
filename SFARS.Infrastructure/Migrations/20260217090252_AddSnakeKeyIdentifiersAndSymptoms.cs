using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSnakeKeyIdentifiersAndSymptoms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "key_identifiers",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "typical_symptoms",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SnakeChangeLog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    field_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    change_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    change_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnakeChangeLog_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_SnakeChangeLog_Snake_snake_id",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Snake_ScientificName",
                table: "Snake",
                column: "scientific_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnakeChangeLog_CreatedAt",
                table: "SnakeChangeLog",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_SnakeChangeLog_SnakeId",
                table: "SnakeChangeLog",
                column: "snake_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SnakeChangeLog");

            migrationBuilder.DropIndex(
                name: "IX_Snake_ScientificName",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "key_identifiers",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "note",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "typical_symptoms",
                table: "Snake");
        }
    }
}
