using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocVault.Infrastructure.Persistence.Configurations;

public class FailedIndexingJobConfiguration : IEntityTypeConfiguration<FailedIndexingJob>
{
    public void Configure(EntityTypeBuilder<FailedIndexingJob> builder)
    {
        builder.ToTable("FailedIndexingJobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.JobId).IsRequired();
        builder.Property(e => e.StoragePath).IsRequired().HasMaxLength(512);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.LastError).IsRequired();
        builder.Property(e => e.AttemptCount).IsRequired();
        builder.Property(e => e.MaxAttempts).IsRequired();
        builder.Property(e => e.FirstFailedAt).IsRequired();
        builder.Property(e => e.LastFailedAt).IsRequired();
        builder.Property(e => e.NextRetryAt);
        builder.Property(e => e.IsExhausted).IsRequired();

        builder.HasIndex(e => e.JobId).IsUnique();
        builder.HasIndex(e => new { e.IsExhausted, e.NextRetryAt });
    }
}
