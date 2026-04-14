using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DocVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Documents",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Embedding",
                table: "Documents",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Embedding",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Documents");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
