using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIdToImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "ImportJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "IndexingError",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "IndexingError",
                table: "Documents");
        }
    }
}
