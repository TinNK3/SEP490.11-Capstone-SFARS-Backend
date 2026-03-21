using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixIncidentSequenceCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER SEQUENCE dbo.IncidentCodeSeq NO CACHE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Default cache for SQL Server is 50
            migrationBuilder.Sql("ALTER SEQUENCE dbo.IncidentCodeSeq CACHE 50;");
        }
    }
}
