using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleCoverMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverMediaId",
                table: "Articles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CoverMediaId",
                table: "Articles",
                column: "CoverMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_MediaAssets_CoverMediaId",
                table: "Articles",
                column: "CoverMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Articles_MediaAssets_CoverMediaId",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_CoverMediaId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CoverMediaId",
                table: "Articles");
        }
    }
}
