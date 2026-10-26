using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class LinkChallengeCardsToSqlChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SqlChallengeId",
                table: "InactiveChallenges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SqlChallengeId",
                table: "CodeChallengeCards",
                type: "int",
                nullable: true);

            // Nettoyage avant la clé étrangère : on supprime les lignes "sql-{id}" dont le
            // défi n'existe plus (ex : sql-1), puis on renseigne SqlChallengeId pour les autres.
            foreach (string table in new[] { "CodeChallengeCards", "InactiveChallenges" })
            {
                migrationBuilder.Sql($@"
DELETE t FROM [{table}] t
WHERE t.ChallengeKey LIKE 'sql-%'
  AND NOT EXISTS (SELECT 1 FROM [SqlChallenges] s WHERE CONCAT('sql-', s.Id) = t.ChallengeKey);");

                migrationBuilder.Sql($@"
UPDATE t SET t.SqlChallengeId = s.Id
FROM [{table}] t
JOIN [SqlChallenges] s ON CONCAT('sql-', s.Id) = t.ChallengeKey;");
            }

            migrationBuilder.CreateIndex(
                name: "IX_InactiveChallenges_SqlChallengeId",
                table: "InactiveChallenges",
                column: "SqlChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeChallengeCards_SqlChallengeId",
                table: "CodeChallengeCards",
                column: "SqlChallengeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CodeChallengeCards_SqlChallenges_SqlChallengeId",
                table: "CodeChallengeCards",
                column: "SqlChallengeId",
                principalTable: "SqlChallenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InactiveChallenges_SqlChallenges_SqlChallengeId",
                table: "InactiveChallenges",
                column: "SqlChallengeId",
                principalTable: "SqlChallenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CodeChallengeCards_SqlChallenges_SqlChallengeId",
                table: "CodeChallengeCards");

            migrationBuilder.DropForeignKey(
                name: "FK_InactiveChallenges_SqlChallenges_SqlChallengeId",
                table: "InactiveChallenges");

            migrationBuilder.DropIndex(
                name: "IX_InactiveChallenges_SqlChallengeId",
                table: "InactiveChallenges");

            migrationBuilder.DropIndex(
                name: "IX_CodeChallengeCards_SqlChallengeId",
                table: "CodeChallengeCards");

            migrationBuilder.DropColumn(
                name: "SqlChallengeId",
                table: "InactiveChallenges");

            migrationBuilder.DropColumn(
                name: "SqlChallengeId",
                table: "CodeChallengeCards");
        }
    }
}
