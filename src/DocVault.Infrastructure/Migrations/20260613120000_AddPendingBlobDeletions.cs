using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingBlobDeletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingBlobDeletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingBlobDeletions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingBlobDeletions_StoragePath",
                table: "PendingBlobDeletions",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingBlobDeletions_AttemptCount",
                table: "PendingBlobDeletions",
                column: "AttemptCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingBlobDeletions");
        }
    }
}
