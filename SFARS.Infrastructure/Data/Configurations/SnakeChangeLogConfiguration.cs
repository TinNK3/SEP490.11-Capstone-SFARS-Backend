using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SnakeChangeLogConfiguration : IEntityTypeConfiguration<SnakeChangeLog>
{
    public void Configure(EntityTypeBuilder<SnakeChangeLog> builder)
    {
        builder.ToTable("SnakeChangeLog");
        builder.HasKey(e => e.Id).HasName("PK_SnakeChangeLog_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SnakeId)
            .IsRequired()
            .HasColumnName("snake_id");

        builder.Property(e => e.FieldName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("field_name");

        builder.Property(e => e.OldValue)
            .HasColumnName("old_value");

        builder.Property(e => e.NewValue)
            .HasColumnName("new_value");

        builder.Property(e => e.ChangeType)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("change_type");

        builder.Property(e => e.ChangeReason)
            .HasMaxLength(500)
            .HasColumnName("change_reason");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // Indexes
        builder.HasIndex(e => e.SnakeId)
            .HasDatabaseName("IX_SnakeChangeLog_SnakeId");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_SnakeChangeLog_CreatedAt");

        // FK
        builder.HasOne(e => e.Snake)
            .WithMany()
            .HasForeignKey(e => e.SnakeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}