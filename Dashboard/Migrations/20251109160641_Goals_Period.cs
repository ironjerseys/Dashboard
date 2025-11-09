using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class Goals_Period : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Goals_OwnerId_WeekStart",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "WeekStart",
                table: "Goals",
                newName: "Fin");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Debut",
                table: "Goals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerId_Debut_Fin",
                table: "Goals",
                columns: new[] { "OwnerId", "Debut", "Fin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Goals_OwnerId_Debut_Fin",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Debut",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "Fin",
                table: "Goals",
                newName: "WeekStart");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerId_WeekStart",
                table: "Goals",
                columns: new[] { "OwnerId", "WeekStart" });
        }
    }
}
