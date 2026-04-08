using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessage");
        builder.HasKey(e => e.Id).HasName("PK_OutboxMessage_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("event_type");

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnName("payload");

        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_processed");

        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");

        builder.Property(e => e.Error)
            .HasColumnName("error");

        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("retry_count");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.IsProcessed)
            .HasDatabaseName("IX_OutboxMessage_IsProcessed");
            
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_OutboxMessage_CreatedAt");
    }
}