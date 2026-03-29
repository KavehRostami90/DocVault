using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for DocVault.
/// Exposes <see cref="DbSet{TEntity}"/> properties for every first-class aggregate
/// and applies all <see cref="IEntityTypeConfiguration{TEntity}"/> implementations
/// discovered in this assembly.
/// </summary>
public class DocVaultDbContext : DbContext
{
  /// <summary>Initialises the context with the supplied options.</summary>
  /// <param name="options">EF Core context options (provider, connection string, etc.).</param>
  public DocVaultDbContext(DbContextOptions options) : base(options) { }

  /// <summary>Documents stored in the vault.</summary>
  public DbSet<Document> Documents => Set<Document>();

  /// <summary>Distinct tag definitions referenced by one or more documents.</summary>
  public DbSet<Tag> Tags => Set<Tag>();

  /// <summary>Import job lifecycle records.</summary>
  public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

  /// <summary>Durable work-queue entries consumed by the background indexing worker.</summary>
  public DbSet<IndexingQueueEntry> IndexingQueue => Set<IndexingQueueEntry>();

  /// <summary>
  /// Applies all <see cref="IEntityTypeConfiguration{TEntity}"/> classes found in this assembly.
  /// On relational providers (PostgreSQL) also adds the stored tsvector column + GIN index
  /// used for full-text search.
  /// </summary>
  /// <param name="modelBuilder">The EF Core model builder.</param>
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocVaultDbContext).Assembly);

    // Stored generated tsvector column — PostgreSQL only; skipped for InMemory (tests/dev)
    if (Database.IsRelational())
    {
      modelBuilder.Entity<Document>()
        .Property<NpgsqlTsVector>("SearchVector")
        .HasColumnType("tsvector")
        .HasComputedColumnSql("to_tsvector('english', coalesce(\"Text\", ''))", stored: true);

      modelBuilder.Entity<Document>()
        .HasIndex("SearchVector")
        .HasMethod("GIN");
    }
  }
}
