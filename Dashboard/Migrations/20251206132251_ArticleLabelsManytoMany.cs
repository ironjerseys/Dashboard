using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class ArticleLabelsManytoMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Labels_LabelId",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_LabelId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "LabelId",
                table: "Articles");

            migrationBuilder.CreateTable(
                name: "ArticleLabel",
                columns: table => new
                {
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    LabelsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLabel", x => new { x.ArticleId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_ArticleLabel_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleLabel_Labels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLabel_LabelsId",
                table: "ArticleLabel",
                column: "LabelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleLabel");

            migrationBuilder.AddColumn<int>(
                name: "LabelId",
                table: "Articles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_LabelId",
                table: "Articles",
                column: "LabelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Labels_LabelId",
                table: "Articles",
                column: "LabelId",
                principalTable: "Labels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
