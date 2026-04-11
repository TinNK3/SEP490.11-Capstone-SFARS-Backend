using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeminiApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsExhausted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExhaustedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalUsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeminiApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeminiApiKeys_IsActive_IsExhausted_LastUsedAt",
                table: "GeminiApiKeys",
                columns: new[] { "IsActive", "IsExhausted", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GeminiApiKeys_KeyValue",
                table: "GeminiApiKeys",
                column: "KeyValue",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeminiApiKeys");
        }
    }
}
