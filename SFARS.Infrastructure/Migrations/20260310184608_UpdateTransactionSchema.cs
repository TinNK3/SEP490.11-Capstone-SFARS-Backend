using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_RescueMission_MissionId",
                table: "Transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_mission_id",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "type",
                table: "Transaction");

            migrationBuilder.RenameColumn(
                name: "payment_gateway",
                table: "Transaction",
                newName: "transaction_code");

            migrationBuilder.RenameColumn(
                name: "gateway_transaction_id",
                table: "Transaction",
                newName: "payment_link_id");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "Transaction",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "Transaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expired_at",
                table: "Transaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_code",
                table: "Transaction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "transaction_date",
                table: "Transaction",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "expired_at",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "qr_code",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "transaction_date",
                table: "Transaction");

            migrationBuilder.RenameColumn(
                name: "transaction_code",
                table: "Transaction",
                newName: "payment_gateway");

            migrationBuilder.RenameColumn(
                name: "payment_link_id",
                table: "Transaction",
                newName: "gateway_transaction_id");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "Transaction",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "Transaction",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_mission_id",
                table: "Transaction",
                column: "mission_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_RescueMission_MissionId",
                table: "Transaction",
                column: "mission_id",
                principalTable: "RescueMission",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
