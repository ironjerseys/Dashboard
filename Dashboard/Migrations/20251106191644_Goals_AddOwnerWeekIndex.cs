using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class Goals_AddOwnerWeekIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalLogs");

            migrationBuilder.DropColumn(
                name: "Cible",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "HeureRappelLocal",
                table: "Goals",
                newName: "ArticleId");

            migrationBuilder.AddColumn<bool>(
                name: "IsDone",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WeekStart",
                table: "Goals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Goals_ArticleId",
                table: "Goals",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerId_WeekStart",
                table: "Goals",
                columns: new[] { "OwnerId", "WeekStart" });

            // Nettoie les valeurs héritées de HeureRappelLocal (0..23) devenues ArticleId invalides
            migrationBuilder.Sql(@"
UPDATE G
SET G.ArticleId = NULL
FROM Goals G
WHERE G.ArticleId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Articles A WHERE A.Id = G.ArticleId);
");

            // Ajoute la FK une fois les données conformes
            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Articles_ArticleId",
                table: "Goals",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Articles_ArticleId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_ArticleId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_OwnerId_WeekStart",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "IsDone",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "WeekStart",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "ArticleId",
                table: "Goals",
                newName: "HeureRappelLocal");

            migrationBuilder.AddColumn<int>(
                name: "Cible",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GoalLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: true),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    CompteRendu = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Valeur = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalLogs_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GoalLogs_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalLogs_ArticleId",
                table: "GoalLogs",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLogs_GoalId_Date",
                table: "GoalLogs",
                columns: new[] { "GoalId", "Date" });
        }
    }
}
