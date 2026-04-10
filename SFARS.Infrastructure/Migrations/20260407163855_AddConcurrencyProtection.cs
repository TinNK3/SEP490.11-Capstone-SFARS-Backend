using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserDevice",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "User",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Transaction",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SystemMessage",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SnakeImage",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SnakeHotspot",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SnakeChangeLog",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Snake",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Role",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Review",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RetrainHistory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RescueMission",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Reports",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Reels",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReelComments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Quiz",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PostMedia",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PostComment",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PointTransaction",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OtpRequest",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NotificationLog",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MedicalFacility",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentSymptoms",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentStatusHistory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentMedia",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentChats",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentChatMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IncidentChatMessageMedias",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Incident",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "FirstAidDetail",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Faqs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContentPost",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChatSessions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChatMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiReviewAuditLog",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiInferenceReview",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiInferenceCandidate",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiInference",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AdminAuditLog",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserDevice");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SnakeImage");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SnakeHotspot");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SnakeChangeLog");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RetrainHistory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RescueMission");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Reels");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReelComments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Quiz");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PostMedia");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PostComment");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PointTransaction");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OtpRequest");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NotificationLog");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MedicalFacility");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentSymptoms");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentStatusHistory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentMedia");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentChats");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentChatMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IncidentChatMessageMedias");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Incident");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "FirstAidDetail");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Faqs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContentPost");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiReviewAuditLog");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiInferenceReview");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiInferenceCandidate");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiInference");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AdminAuditLog");
        }
    }
}
