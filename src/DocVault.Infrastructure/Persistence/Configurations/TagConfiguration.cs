using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="Tag"/> entity.
/// </summary>
public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
  /// <summary>Applies the <see cref="Tag"/> entity configuration to the model builder.</summary>
  /// <param name="builder">The entity type builder for <see cref="Tag"/>.</param>
  public void Configure(EntityTypeBuilder<Tag> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Name).HasMaxLength(64);
  }
}
