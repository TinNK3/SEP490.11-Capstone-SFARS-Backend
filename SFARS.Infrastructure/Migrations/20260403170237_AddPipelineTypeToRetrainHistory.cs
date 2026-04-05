using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineTypeToRetrainHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pipeline_type",
                table: "RetrainHistory",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RetrainHistory_PipelineType_Status",
                table: "RetrainHistory",
                columns: new[] { "pipeline_type", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetrainHistory_PipelineType_Status",
                table: "RetrainHistory");

            migrationBuilder.DropColumn(
                name: "pipeline_type",
                table: "RetrainHistory");
        }
    }
}
