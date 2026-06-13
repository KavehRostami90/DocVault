using DocVault.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public class PendingBlobDeletionConfiguration : IEntityTypeConfiguration<PendingBlobDeletion>
{
  public void Configure(EntityTypeBuilder<PendingBlobDeletion> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
    builder.Property(x => x.LastError).HasMaxLength(1024);
    builder.HasIndex(x => x.StoragePath).IsUnique();
    builder.HasIndex(x => x.AttemptCount);
  }
}
