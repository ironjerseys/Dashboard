using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddScraperRolesAndHiddenJobCharts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiddenJobCharts",
                columns: table => new
                {
                    ChartKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenJobCharts", x => x.ChartKey);
                });

            migrationBuilder.CreateTable(
                name: "ScraperRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SearchTerm = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GooglePhrase = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScraperRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScraperRoles_SearchTerm",
                table: "ScraperRoles",
                column: "SearchTerm",
                unique: true);

            // Postes qui étaient codés en dur dans jobspy-scraper/scrape_jobs.py (mêmes phrases Google).
            // EXEC : compilé à l'exécution, pour que le script idempotent (un seul batch) reste valide.
            migrationBuilder.Sql(@"
EXEC(N'INSERT INTO [ScraperRoles] (SearchTerm, GooglePhrase, CreatedUtc) VALUES
(N''.NET developer'', N''.NET developer C# jobs near'', SYSUTCDATETIME()),
(N''DevOps Kubernetes'', N''DevOps Kubernetes engineer jobs near'', SYSUTCDATETIME()),
(N''ML engineer'', N''Machine Learning engineer jobs near'', SYSUTCDATETIME()),
(N''GRC Governance Risk Compliance'', N''GRC Governance Risk Compliance cybersecurity jobs near'', SYSUTCDATETIME()),
(N''Business analyst'', N''Business analyst jobs near'', SYSUTCDATETIME()),
(N''Analyste fonctionnel'', N''Analyste fonctionnel emploi'', SYSUTCDATETIME()),
(N''Support applicatif'', N''Support applicatif emploi'', SYSUTCDATETIME());');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiddenJobCharts");

            migrationBuilder.DropTable(
                name: "ScraperRoles");
        }
    }
}
