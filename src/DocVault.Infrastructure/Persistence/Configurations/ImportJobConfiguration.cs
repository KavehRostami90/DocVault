using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="ImportJob"/> aggregate root.
/// Enforces column lengths, required fields, and adds an index on <c>Status</c>
/// to optimise the crash-recovery scan performed at startup.
/// </summary>
public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
  /// <summary>Applies the <see cref="ImportJob"/> entity configuration to the model builder.</summary>
  /// <param name="builder">The entity type builder for <see cref="ImportJob"/>.</param>
  public void Configure(EntityTypeBuilder<ImportJob> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.DocumentId)
      .HasConversion(id => id.Value, value => new DocumentId(value))
      .IsRequired();
    builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
    builder.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
    builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
    builder.Property(x => x.Status).IsRequired();
    builder.HasIndex(x => x.Status); // speed up the recovery scan
  }
}
