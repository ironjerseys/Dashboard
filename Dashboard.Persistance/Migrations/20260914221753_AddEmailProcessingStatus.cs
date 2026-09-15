using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailProcessingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabelSyncError",
                table: "EmailMessages",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LabelSyncPending",
                table: "EmailMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedUtc",
                table: "EmailMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "EmailMessages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                // Les mails deja en base n'ont jamais ete regardes : ils partent en attente.
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_LabelSyncPending",
                table: "EmailMessages",
                column: "LabelSyncPending");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_Status",
                table: "EmailMessages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_LabelSyncPending",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_Status",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "LabelSyncError",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "LabelSyncPending",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ProcessedUtc",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "EmailMessages");
        }
    }
}
