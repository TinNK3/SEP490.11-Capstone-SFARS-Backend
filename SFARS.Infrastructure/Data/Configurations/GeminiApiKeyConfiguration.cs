using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class GeminiApiKeyConfiguration : IEntityTypeConfiguration<GeminiApiKey>
{
    public void Configure(EntityTypeBuilder<GeminiApiKey> builder)
    {
        builder.ToTable("GeminiApiKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(k => k.Label)
            .HasMaxLength(200);

        builder.Property(k => k.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(k => k.IsExhausted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.TotalUsageCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(k => k.ConsecutiveFailures)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(k => k.KeyValue)
            .IsUnique();

        // Composite index for runtime key acquisition query
        builder.HasIndex(k => new { k.IsActive, k.IsExhausted, k.LastUsedAt });
    }
}