using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDataV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPostingsV2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Site = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    JobUrl = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
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
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostingsV2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobScrapeRunsV2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsBackfill = table.Column<bool>(type: "bit", nullable: false),
                    HoursOld = table.Column<int>(type: "int", nullable: false),
                    ResultsPerSite = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobScrapeRunsV2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobScrapeSearchesV2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsCapped = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobScrapeSearchesV2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobScrapeSearchesV2_JobScrapeRunsV2_RunId",
                        column: x => x.RunId,
                        principalTable: "JobScrapeRunsV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobSightingsV2",
                columns: table => new
                {
                    SearchId = table.Column<int>(type: "int", nullable: false),
                    JobPostingId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSightingsV2", x => new { x.SearchId, x.JobPostingId });
                    table.ForeignKey(
                        name: "FK_JobSightingsV2_JobPostingsV2_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostingsV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobSightingsV2_JobScrapeSearchesV2_SearchId",
                        column: x => x.SearchId,
                        principalTable: "JobScrapeSearchesV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostingsV2_JobUrl",
                table: "JobPostingsV2",
                column: "JobUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobScrapeRunsV2_StartedUtc",
                table: "JobScrapeRunsV2",
                column: "StartedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JobScrapeSearchesV2_RunId_Role_City_Site",
                table: "JobScrapeSearchesV2",
                columns: new[] { "RunId", "Role", "City", "Site" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSightingsV2_JobPostingId",
                table: "JobSightingsV2",
                column: "JobPostingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSightingsV2");

            migrationBuilder.DropTable(
                name: "JobPostingsV2");

            migrationBuilder.DropTable(
                name: "JobScrapeSearchesV2");

            migrationBuilder.DropTable(
                name: "JobScrapeRunsV2");
        }
    }
}
