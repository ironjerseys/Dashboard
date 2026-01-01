using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class RenameAndAddPublicFieldQuestionTechnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "QuizQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionTechniqueId",
                table: "Labels",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labels_QuestionTechniqueId",
                table: "Labels",
                column: "QuestionTechniqueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Labels_QuizQuestions_QuestionTechniqueId",
                table: "Labels",
                column: "QuestionTechniqueId",
                principalTable: "QuizQuestions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labels_QuizQuestions_QuestionTechniqueId",
                table: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_Labels_QuestionTechniqueId",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionTechniqueId",
                table: "Labels");
        }
    }
}
