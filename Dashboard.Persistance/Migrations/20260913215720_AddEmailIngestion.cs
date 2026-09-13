using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Uid = table.Column<long>(type: "bigint", nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    Folder = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SentUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IngestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailboxSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Account = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Folder = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    LastSeenUid = table.Column<long>(type: "bigint", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxSyncStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_FromAddress",
                table: "EmailMessages",
                column: "FromAddress");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MessageId",
                table: "EmailMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_SentUtc",
                table: "EmailMessages",
                column: "SentUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxSyncStates_Account_Folder",
                table: "MailboxSyncStates",
                columns: new[] { "Account", "Folder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "MailboxSyncStates");
        }
    }
}
