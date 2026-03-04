using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence;

public class DocVaultDbContext : DbContext
{
  public DocVaultDbContext(DbContextOptions options) : base(options) { }

  public DbSet<Document> Documents => Set<Document>();
  public DbSet<Tag> Tags => Set<Tag>();
  public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
  public DbSet<IndexingQueueEntry> IndexingQueue => Set<IndexingQueueEntry>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocVaultDbContext).Assembly);
  }
}
