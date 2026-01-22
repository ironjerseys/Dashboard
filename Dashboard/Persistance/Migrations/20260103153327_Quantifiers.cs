using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class Quantifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quantifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quantifiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuantifierEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuantifierId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuantifierEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantifierEntries_Quantifiers_QuantifierId",
                        column: x => x.QuantifierId,
                        principalTable: "Quantifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantifierEntries_QuantifierId_Date",
                table: "QuantifierEntries",
                columns: new[] { "QuantifierId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quantifiers_UserId_Name",
                table: "Quantifiers",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuantifierEntries");

            migrationBuilder.DropTable(
                name: "Quantifiers");
        }
    }
}
