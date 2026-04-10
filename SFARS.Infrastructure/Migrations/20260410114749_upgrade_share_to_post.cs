using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class upgrade_share_to_post : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShareCount",
                table: "ContentPost",
                newName: "share_count");

            migrationBuilder.AlterColumn<int>(
                name: "share_count",
                table: "ContentPost",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "SharedPostId",
                table: "ContentPost",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedReelId",
                table: "ContentPost",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentPost_SharedPostId",
                table: "ContentPost",
                column: "SharedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPost_SharedReelId",
                table: "ContentPost",
                column: "SharedReelId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentPost_ContentPost_SharedPostId",
                table: "ContentPost",
                column: "SharedPostId",
                principalTable: "ContentPost",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentPost_Reels_SharedReelId",
                table: "ContentPost",
                column: "SharedReelId",
                principalTable: "Reels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentPost_ContentPost_SharedPostId",
                table: "ContentPost");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentPost_Reels_SharedReelId",
                table: "ContentPost");

            migrationBuilder.DropIndex(
                name: "IX_ContentPost_SharedPostId",
                table: "ContentPost");

            migrationBuilder.DropIndex(
                name: "IX_ContentPost_SharedReelId",
                table: "ContentPost");

            migrationBuilder.DropColumn(
                name: "SharedPostId",
                table: "ContentPost");

            migrationBuilder.DropColumn(
                name: "SharedReelId",
                table: "ContentPost");

            migrationBuilder.RenameColumn(
                name: "share_count",
                table: "ContentPost",
                newName: "ShareCount");

            migrationBuilder.AlterColumn<int>(
                name: "ShareCount",
                table: "ContentPost",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }
    }
}
