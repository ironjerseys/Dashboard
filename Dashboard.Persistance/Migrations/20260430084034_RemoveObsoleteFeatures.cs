using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIChessLogs");

            migrationBuilder.DropTable(
                name: "EmailSettings");

            migrationBuilder.DropTable(
                name: "Todos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIChessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BestMoveUci = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BestScoreCp = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    EvaluatedMovesCount = table.Column<int>(type: "int", nullable: false),
                    EvaluatedMovesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedMovesTotal = table.Column<long>(type: "bigint", nullable: false),
                    LeafEvaluations = table.Column<long>(type: "bigint", nullable: false),
                    LegalMovesCount = table.Column<int>(type: "int", nullable: false),
                    NodesVisited = table.Column<long>(type: "bigint", nullable: false),
                    SearchDepth = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIChessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    Hour = table.Column<int>(type: "int", nullable: false),
                    IncludeArticles = table.Column<bool>(type: "bit", nullable: false),
                    IncludeGoals = table.Column<bool>(type: "bit", nullable: false),
                    IncludeTodos = table.Column<bool>(type: "bit", nullable: false),
                    LastSentUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Todos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDone = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Todos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIChessLogs_TimestampUtc",
                table: "AIChessLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSettings_UserId",
                table: "EmailSettings",
                column: "UserId",
                unique: true);
        }
    }
}
