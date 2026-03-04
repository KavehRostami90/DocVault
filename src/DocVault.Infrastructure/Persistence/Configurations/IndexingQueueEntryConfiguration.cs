using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public sealed class IndexingQueueEntryConfiguration : IEntityTypeConfiguration<IndexingQueueEntry>
{
  public void Configure(EntityTypeBuilder<IndexingQueueEntry> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).UseIdentityAlwaysColumn();
    builder.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
    builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
    // Index on EnqueuedAt so the SKIP LOCKED ORDER BY is satisfied by the index scan.
    builder.HasIndex(x => x.EnqueuedAt);
  }
}
