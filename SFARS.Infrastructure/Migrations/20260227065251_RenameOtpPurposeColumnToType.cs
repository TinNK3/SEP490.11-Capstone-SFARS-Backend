using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOtpPurposeColumnToType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "purpose",
                table: "OtpRequest",
                newName: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "type",
                table: "OtpRequest",
                newName: "purpose");
        }
    }
}
