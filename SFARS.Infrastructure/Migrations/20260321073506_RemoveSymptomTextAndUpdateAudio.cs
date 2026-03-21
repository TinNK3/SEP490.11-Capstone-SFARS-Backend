using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSymptomTextAndUpdateAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SymptomText",
                table: "Incident");

            migrationBuilder.RenameColumn(
                name: "SymptomAudioUrl",
                table: "Incident",
                newName: "symptom_audio_url");

            migrationBuilder.AlterColumn<string>(
                name: "symptom_audio_url",
                table: "Incident",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "symptom_audio_url",
                table: "Incident",
                newName: "SymptomAudioUrl");

            migrationBuilder.AlterColumn<string>(
                name: "SymptomAudioUrl",
                table: "Incident",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SymptomText",
                table: "Incident",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
