using DocVault.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
  public void Configure(EntityTypeBuilder<ApiKey> b)
  {
    b.HasKey(k => k.Id);
    b.Property(k => k.Name).HasMaxLength(100).IsRequired();
    b.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
    b.Property(k => k.KeyPrefix).HasMaxLength(16).IsRequired();
    b.Property(k => k.UserId).IsRequired();
    b.HasIndex(k => k.KeyHash).IsUnique();
    b.HasIndex(k => k.UserId);
  }
}
