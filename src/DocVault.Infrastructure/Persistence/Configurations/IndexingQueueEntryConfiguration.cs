using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="IndexingQueueEntry"/> table.
/// Uses an <c>IDENTITY ALWAYS</c> surrogate key for FIFO ordering and adds an
/// index on <c>EnqueuedAt</c> to support the <c>SKIP LOCKED ORDER BY</c> query.
/// </summary>
public sealed class IndexingQueueEntryConfiguration : IEntityTypeConfiguration<IndexingQueueEntry>
{
  /// <summary>Applies the <see cref="IndexingQueueEntry"/> configuration to the model builder.</summary>
  /// <param name="builder">The entity type builder for <see cref="IndexingQueueEntry"/>.</param>
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
