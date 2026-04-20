using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(c => c.Id);

        // DocumentId is a value-object wrapping Guid — use the same conversion as Document.Id
        // so EF can resolve the FK relationship without a type mismatch.
        builder.Property(c => c.DocumentId)
            .HasConversion(id => id.Value, value => new DocumentId(value))
            .IsRequired();

        builder.Property(c => c.ChunkIndex).IsRequired();
        builder.Property(c => c.Text).HasColumnType("text").IsRequired();
        builder.Property(c => c.StartChar).IsRequired();
        builder.Property(c => c.EndChar).IsRequired();

        builder.HasIndex(c => c.DocumentId);

        // FK → Documents with cascade delete so chunks are cleaned up when a document is deleted.
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // pgvector column + HNSW index are relational-only and configured in DocVaultDbContext.OnModelCreating.
    }
}
