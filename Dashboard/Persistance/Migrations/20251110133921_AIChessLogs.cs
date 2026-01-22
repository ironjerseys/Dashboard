using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AIChessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIChessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SearchDepth = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    LegalMovesCount = table.Column<int>(type: "int", nullable: false),
                    EvaluatedMovesCount = table.Column<int>(type: "int", nullable: false),
                    BestMoveUci = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BestScoreCp = table.Column<int>(type: "int", nullable: true),
                    EvaluatedMovesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIChessLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIChessLogs_TimestampUtc",
                table: "AIChessLogs",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIChessLogs");
        }
    }
}
