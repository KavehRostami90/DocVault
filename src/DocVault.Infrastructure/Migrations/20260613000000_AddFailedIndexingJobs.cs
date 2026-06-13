using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedIndexingJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedIndexingJobs",
                columns: table => new
                {
                    Id            = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId         = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePath   = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType   = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AttemptCount  = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts   = table.Column<int>(type: "integer", nullable: false),
                    LastError     = table.Column<string>(type: "text", nullable: false),
                    FirstFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextRetryAt   = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsExhausted   = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedIndexingJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedIndexingJobs_JobId",
                table: "FailedIndexingJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedIndexingJobs_IsExhausted_NextRetryAt",
                table: "FailedIndexingJobs",
                columns: new[] { "IsExhausted", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FailedIndexingJobs");
        }
    }
}
