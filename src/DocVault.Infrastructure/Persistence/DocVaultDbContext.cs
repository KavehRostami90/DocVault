using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Pgvector;

namespace DocVault.Infrastructure.Persistence;

public class DocVaultDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
  public DocVaultDbContext(DbContextOptions options) : base(options) { }

  public DbSet<Document> Documents => Set<Document>();
  public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
  public DbSet<Tag> Tags => Set<Tag>();
  public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
  public DbSet<IndexingQueueEntry> IndexingQueue => Set<IndexingQueueEntry>();
  public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocVaultDbContext).Assembly);

    modelBuilder.Entity<RefreshToken>(b =>
    {
      b.HasKey(t => t.Id);
      b.Property(t => t.Token).HasMaxLength(512).IsRequired();
      b.HasIndex(t => t.Token).IsUnique();
      b.HasIndex(t => t.UserId);
    });

    // Relational-only configuration — PostgreSQL only; skipped for InMemory (tests/dev)
    if (Database.IsRelational())
    {
      // Full-text search: stored generated tsvector column + GIN index
      modelBuilder.Entity<Document>()
        .Property<NpgsqlTsVector>("SearchVector")
        .HasColumnType("tsvector")
        .HasComputedColumnSql("to_tsvector('english', coalesce(\"Text\", ''))", stored: true);

      modelBuilder.Entity<Document>()
        .HasIndex("SearchVector")
        .HasMethod("GIN");

      // pgvector: declare the extension, map the Embedding column, and add an HNSW index
      // for efficient cosine similarity search.
      modelBuilder.HasPostgresExtension("vector");

      modelBuilder.Entity<Document>()
        .Property(d => d.Embedding)
        .HasConversion(
          v => v != null ? new Vector(v) : null,
          v => v != null ? v.Memory.ToArray() : null)
        .HasColumnType("vector(768)");

      modelBuilder.Entity<Document>()
        .HasIndex(d => d.Embedding)
        .HasMethod("hnsw")
        .HasOperators("vector_cosine_ops");

      // DocumentChunk: pgvector column and HNSW index for chunk-level semantic search.
      modelBuilder.Entity<DocumentChunk>()
        .Property(c => c.Embedding)
        .HasConversion(
          v => v != null ? new Vector(v) : null,
          v => v != null ? v.Memory.ToArray() : null)
        .HasColumnType("vector(768)");

      modelBuilder.Entity<DocumentChunk>()
        .HasIndex(c => c.Embedding)
        .HasMethod("hnsw")
        .HasOperators("vector_cosine_ops");
    }
  }
}
