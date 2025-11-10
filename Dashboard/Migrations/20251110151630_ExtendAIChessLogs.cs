using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAIChessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GeneratedMovesTotal",
                table: "AIChessLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LeafEvaluations",
                table: "AIChessLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NodesVisited",
                table: "AIChessLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedMovesTotal",
                table: "AIChessLogs");

            migrationBuilder.DropColumn(
                name: "LeafEvaluations",
                table: "AIChessLogs");

            migrationBuilder.DropColumn(
                name: "NodesVisited",
                table: "AIChessLogs");
        }
    }
}
