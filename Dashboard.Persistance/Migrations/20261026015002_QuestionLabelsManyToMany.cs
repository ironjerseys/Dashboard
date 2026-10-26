using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class QuestionLabelsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionLabels",
                columns: table => new
                {
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    LabelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionLabels", x => new { x.QuestionId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_QuestionLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionLabels_QuizQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionLabels_LabelId",
                table: "QuestionLabels",
                column: "LabelId");

            // Reprise des liens existants (Labels.QuestionTechniqueId) avant de supprimer la colonne.
            migrationBuilder.Sql(@"
INSERT INTO [QuestionLabels] (QuestionId, LabelId)
SELECT l.QuestionTechniqueId, l.Id
FROM [Labels] l
WHERE l.QuestionTechniqueId IS NOT NULL
  AND EXISTS (SELECT 1 FROM [QuizQuestions] q WHERE q.Id = l.QuestionTechniqueId);");

            migrationBuilder.DropForeignKey(
                name: "FK_Labels_QuizQuestions_QuestionTechniqueId",
                table: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_Labels_QuestionTechniqueId",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "QuestionTechniqueId",
                table: "Labels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionLabels");

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
    }
}
