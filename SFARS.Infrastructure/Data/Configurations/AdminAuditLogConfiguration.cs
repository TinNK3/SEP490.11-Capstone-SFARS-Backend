using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLog");
        builder.HasKey(e => e.Id).HasName("PK_AdminAuditLog_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.AdminId)
            .IsRequired()
            .HasColumnName("admin_id");

        builder.Property(e => e.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("action");

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("entity_type");

        builder.Property(e => e.EntityId)
            .IsRequired()
            .HasColumnName("entity_id");

        builder.Property(e => e.OldValue)
            .HasColumnType("text")
            .HasColumnName("old_value");

        builder.Property(e => e.NewValue)
            .HasColumnType("text")
            .HasColumnName("new_value");

        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .HasColumnName("reason");

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45)
            .HasColumnName("ip_address");

        // Audit columns (BaseEntity)
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // Indexes
        builder.HasIndex(e => new { e.AdminId, e.CreatedAt })
            .HasDatabaseName("IX_AdminAuditLog_AdminId_CreatedAt");

        builder.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("IX_AdminAuditLog_EntityType_EntityId");

        builder.HasIndex(e => e.Action)
            .HasDatabaseName("IX_AdminAuditLog_Action");

        // Relationships
        builder.HasOne(a => a.Admin)
            .WithMany()
            .HasForeignKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AdminAuditLog_User_AdminId");
    }
}
