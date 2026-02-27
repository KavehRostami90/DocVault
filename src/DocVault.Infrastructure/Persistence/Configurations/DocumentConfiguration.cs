using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
  public void Configure(EntityTypeBuilder<Document> builder)
  {
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id)
      .HasConversion(id => id.Value, value => new DocumentId(value));
    builder.Property(x => x.Title).HasMaxLength(256);
    builder.Property(x => x.FileName).HasMaxLength(256);
    builder.Property(x => x.ContentType).HasMaxLength(128);
    builder.Property(x => x.Hash)
      .HasConversion(hash => hash.Value, value => new FileHash(value));
    builder.Property(x => x.Text).HasColumnType("text");
    builder.HasMany(x => x.Tags)
      .WithMany();
  }
}
