using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Quizresultandhistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quiz_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    score = table.Column<int>(type: "int", nullable: false),
                    total_questions = table.Column<int>(type: "int", nullable: false),
                    points_earned = table.Column<int>(type: "int", nullable: false),
                    is_passed = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_histories_id", x => x.id);
                    table.ForeignKey(
                        name: "FK_quiz_histories_Quiz_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "Quiz",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_quiz_histories_User_user_id",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "quiz_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quiz_history_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    is_correct = table.Column<bool>(type: "bit", nullable: false),
                    selected_option_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    answer_data = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_results_id", x => x.id);
                    table.ForeignKey(
                        name: "FK_quiz_results_QuizQuestion_question_id",
                        column: x => x.question_id,
                        principalTable: "QuizQuestion",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_quiz_results_quiz_histories_quiz_history_id",
                        column: x => x.quiz_history_id,
                        principalTable: "quiz_histories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_histories_quiz_id",
                table: "quiz_histories",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_histories_user_id",
                table: "quiz_histories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_results_question_id",
                table: "quiz_results",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_results_quiz_history_id",
                table: "quiz_results",
                column: "quiz_history_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_results");

            migrationBuilder.DropTable(
                name: "quiz_histories");
        }
    }
}
