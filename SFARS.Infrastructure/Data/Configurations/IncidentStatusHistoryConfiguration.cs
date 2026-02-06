using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class IncidentStatusHistoryConfiguration : IEntityTypeConfiguration<IncidentStatusHistory>
{
    public void Configure(EntityTypeBuilder<IncidentStatusHistory> builder)
    {
        builder.ToTable("IncidentStatusHistory");
        builder.HasKey(e => e.Id).HasName("PK_IncidentStatusHistory_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IncidentId).HasColumnName("incident_id");

        builder.Property(e => e.StatusFrom)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status_from");

        builder.Property(e => e.StatusTo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status_to");

        builder.Property(e => e.ChangedBy)
            .IsRequired()
            .HasColumnName("changed_by");

        builder.Property(e => e.ChangeReason)
            .HasMaxLength(255)
            .HasColumnName("change_reason");

        // audit columns (BaseEntity)
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.IncidentId).HasDatabaseName("IX_IncidentStatusHistory_IncidentId");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_IncidentStatusHistory_CreatedAt");

        builder.HasOne(h => h.Incident)
            .WithMany(i => i.StatusHistories)
            .HasForeignKey(h => h.IncidentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_IncidentStatusHistory_Incident_IncidentId");
    }
}