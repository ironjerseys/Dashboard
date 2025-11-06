using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    public partial class SimplifyGoals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop GoalLogs table if exists
            migrationBuilder.DropTable(name: "GoalLogs");

            // Alter Goals: drop columns Kind, Cible, HeureRappelLocal; add WeekStart, IsDone, ArticleId
            migrationBuilder.DropColumn(name: "Kind", table: "Goals");
            migrationBuilder.DropColumn(name: "Cible", table: "Goals");
            migrationBuilder.DropColumn(name: "HeureRappelLocal", table: "Goals");

            migrationBuilder.AddColumn<DateOnly>(
                name: "WeekStart",
                table: "Goals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2000, 1, 3)); // Monday baseline

            migrationBuilder.AddColumn<bool>(
                name: "IsDone",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArticleId",
                table: "Goals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_ArticleId",
                table: "Goals",
                column: "ArticleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Articles_ArticleId",
                table: "Goals",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Articles_ArticleId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_ArticleId",
                table: "Goals");

            migrationBuilder.DropColumn(name: "ArticleId", table: "Goals");
            migrationBuilder.DropColumn(name: "IsDone", table: "Goals");
            migrationBuilder.DropColumn(name: "WeekStart", table: "Goals");

            migrationBuilder.AddColumn<int>(name: "Kind", table: "Goals", type: "int", nullable: false, defaultValue: 2);
            migrationBuilder.AddColumn<int>(name: "Cible", table: "Goals", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(name: "HeureRappelLocal", table: "Goals", type: "int", nullable: true);

            migrationBuilder.CreateTable(
                name: "GoalLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    ArticleId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Valeur = table.Column<int>(type: "int", nullable: false),
                    CompteRendu = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalLogs", x => x.Id);
                });
        }
    }
}
