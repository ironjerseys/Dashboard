using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Site = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    JobUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    JobUrlDirect = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DatePosted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JobType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Interval = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IsRemote = table.Column<bool>(type: "bit", nullable: true),
                    JobLevel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SearchRole = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SearchCity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScrapedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_JobUrl",
                table: "JobPostings",
                column: "JobUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_ScrapedAt",
                table: "JobPostings",
                column: "ScrapedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_SearchCity",
                table: "JobPostings",
                column: "SearchCity");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_SearchRole",
                table: "JobPostings",
                column: "SearchRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobPostings");
        }
    }
}
