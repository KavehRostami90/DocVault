using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId    = table.Column<string>(type: "text", nullable: false),
                    Name      = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyHash   = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRevoked  = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApiKeys");
        }
    }
}
