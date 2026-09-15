using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplicationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "EmailMessages",
                newName: "AnalysisState");

            migrationBuilder.RenameColumn(
                name: "ProcessedUtc",
                table: "EmailMessages",
                newName: "AnalyzedUtc");

            migrationBuilder.RenameIndex(
                name: "IX_EmailMessages_Status",
                table: "EmailMessages",
                newName: "IX_EmailMessages_AnalysisState");

            // L'ancien statut manuel (Pending/Processed/Ignored) n'a pas d'equivalent : aucun mail n'a
            // encore ete analyse, tout repart en file. Les libelles deja poses a la main seront
            // simplement reposes apres l'analyse.
            migrationBuilder.Sql(
                "UPDATE EmailMessages SET AnalysisState = 'Pending', AnalyzedUtc = NULL, " +
                "LabelSyncPending = 0, LabelSyncError = NULL;");

            migrationBuilder.AddColumn<int>(
                name: "AnalysisAttempts",
                table: "EmailMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisError",
                table: "EmailMessages",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisSummary",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "EmailMessages",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedCompany",
                table: "EmailMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedLocation",
                table: "EmailMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedPosition",
                table: "EmailMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JobApplicationId",
                table: "EmailMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "EmailMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EmailMessageId = table.Column<int>(type: "int", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheCreationInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastEventUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_JobApplicationId",
                table: "EmailMessages",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_NeedsReview",
                table: "EmailMessages",
                column: "NeedsReview");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_TimestampUtc",
                table: "AiUsageRecords",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Company",
                table: "JobApplications",
                column: "Company");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Status",
                table: "JobApplications",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailMessages_JobApplications_JobApplicationId",
                table: "EmailMessages",
                column: "JobApplicationId",
                principalTable: "JobApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailMessages_JobApplications_JobApplicationId",
                table: "EmailMessages");

            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_JobApplicationId",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_NeedsReview",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "AnalysisAttempts",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "AnalysisError",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "AnalysisSummary",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ExtractedCompany",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ExtractedLocation",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ExtractedPosition",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "JobApplicationId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "EmailMessages");

            migrationBuilder.RenameColumn(
                name: "AnalyzedUtc",
                table: "EmailMessages",
                newName: "ProcessedUtc");

            migrationBuilder.RenameColumn(
                name: "AnalysisState",
                table: "EmailMessages",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_EmailMessages_AnalysisState",
                table: "EmailMessages",
                newName: "IX_EmailMessages_Status");
        }
    }
}
