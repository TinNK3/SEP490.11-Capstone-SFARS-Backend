using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureRetrainHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RetrainHistories",
                table: "RetrainHistories");

            migrationBuilder.RenameTable(
                name: "RetrainHistories",
                newName: "RetrainHistory");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "RetrainHistory",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "RetrainHistory",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "RetrainHistory",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "RetrainHistory",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TotalSamplesProcessed",
                table: "RetrainHistory",
                newName: "total_samples_processed");

            migrationBuilder.RenameColumn(
                name: "StartedAt",
                table: "RetrainHistory",
                newName: "started_at");

            migrationBuilder.RenameColumn(
                name: "OldAccuracy",
                table: "RetrainHistory",
                newName: "old_accuracy");

            migrationBuilder.RenameColumn(
                name: "NewAccuracy",
                table: "RetrainHistory",
                newName: "new_accuracy");

            migrationBuilder.RenameColumn(
                name: "ModelVersion",
                table: "RetrainHistory",
                newName: "model_version");

            migrationBuilder.RenameColumn(
                name: "IsPromoted",
                table: "RetrainHistory",
                newName: "is_promoted");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "RetrainHistory",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "DatasetVersion",
                table: "RetrainHistory",
                newName: "dataset_version");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "RetrainHistory",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "RetrainHistory",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "RetrainHistory",
                newName: "completed_at");

            migrationBuilder.AlterColumn<string>(
                name: "model_version",
                table: "RetrainHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "error_message",
                table: "RetrainHistory",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "dataset_version",
                table: "RetrainHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RetrainHistory_Id",
                table: "RetrainHistory",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RetrainHistory_Id",
                table: "RetrainHistory");

            migrationBuilder.RenameTable(
                name: "RetrainHistory",
                newName: "RetrainHistories");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "RetrainHistories",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "RetrainHistories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "RetrainHistories",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "RetrainHistories",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "total_samples_processed",
                table: "RetrainHistories",
                newName: "TotalSamplesProcessed");

            migrationBuilder.RenameColumn(
                name: "started_at",
                table: "RetrainHistories",
                newName: "StartedAt");

            migrationBuilder.RenameColumn(
                name: "old_accuracy",
                table: "RetrainHistories",
                newName: "OldAccuracy");

            migrationBuilder.RenameColumn(
                name: "new_accuracy",
                table: "RetrainHistories",
                newName: "NewAccuracy");

            migrationBuilder.RenameColumn(
                name: "model_version",
                table: "RetrainHistories",
                newName: "ModelVersion");

            migrationBuilder.RenameColumn(
                name: "is_promoted",
                table: "RetrainHistories",
                newName: "IsPromoted");

            migrationBuilder.RenameColumn(
                name: "error_message",
                table: "RetrainHistories",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "dataset_version",
                table: "RetrainHistories",
                newName: "DatasetVersion");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "RetrainHistories",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "RetrainHistories",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "RetrainHistories",
                newName: "CompletedAt");

            migrationBuilder.AlterColumn<string>(
                name: "ModelVersion",
                table: "RetrainHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "RetrainHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetVersion",
                table: "RetrainHistories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RetrainHistories",
                table: "RetrainHistories",
                column: "Id");
        }
    }
}
