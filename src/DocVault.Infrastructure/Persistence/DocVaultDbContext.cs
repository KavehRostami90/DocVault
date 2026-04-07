using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DocVault.Infrastructure.Persistence;

public class DocVaultDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
  public DocVaultDbContext(DbContextOptions options) : base(options) { }

  public DbSet<Document> Documents => Set<Document>();
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
