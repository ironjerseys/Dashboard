using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRoleFiltersV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRoleFiltersV2",
                columns: table => new
                {
                    Role = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IncludeTerms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ExcludeTerms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoleFiltersV2", x => x.Role);
                });

            // Filtres de départ (modifiables depuis /job-data-v2). Un poste sans ligne n'est pas filtré.
            migrationBuilder.Sql(@"
EXEC(N'INSERT INTO [JobRoleFiltersV2] (Role, IncludeTerms, ExcludeTerms, UpdatedUtc) VALUES
(N''.NET developer'', REPLACE(N''.net|.netcore|dotnet|c#'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''DevOps Kubernetes'', REPLACE(N''devops|kubernetes|k8s|site reliability|sre|platform engineer|ingénieur plateforme'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''ML engineer'', REPLACE(N''machine learning|ml|mlops|deep learning|ai engineer|ingénieur ia|ingénieur ai|computer vision|nlp|llm'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''GRC Governance Risk Compliance'', REPLACE(N''grc|gouvernance|governance|compliance|conformité|risque cyber|cyber risk|risques informatiques|it risk|iso 27001|sécurité de l''''information|information security|rssi|ciso'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''Business analyst'', REPLACE(N''business analyst|business systems|ba|analyste d''''affaire|analyste d''''aff|analyste affaire|analyste principal d''''affaire|analyste principale d''''affaire|analyste principal(e) d''''affaire|analyste principal.e d''''affaire|systèmes d''''affaire|analyse d''''affaire'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''Analyste fonctionnel'', REPLACE(N''analyste fonctionnel|analyste fonctionnelle|functional analyst|consultant fonctionnel|consultante fonctionnelle|amoa'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''Support applicatif'', REPLACE(N''support applicatif|application support|applications support|support application|support fonctionnel|production support|analyste support|ingénieur support|technicien support'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME()),
(N''Product Owner'', REPLACE(N''product owner|po|propriétaire de produit|propriétaire produit'', N''|'', NCHAR(10)), NULL, SYSUTCDATETIME());');");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRoleFiltersV2");
        }
    }
}
