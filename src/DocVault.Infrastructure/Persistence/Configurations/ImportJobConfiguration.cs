using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
  public void Configure(EntityTypeBuilder<ImportJob> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.FileName).HasMaxLength(256);
  }
}
