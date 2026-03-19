using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class AiReviewAuditLogConfiguration : IEntityTypeConfiguration<AiReviewAuditLog>
{
    public void Configure(EntityTypeBuilder<AiReviewAuditLog> builder)
    {
        builder.ToTable("AiReviewAuditLog");
        builder.HasKey(e => e.Id).HasName("PK_AiReviewAuditLog_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IncidentId).HasColumnName("incident_id");
        builder.Property(e => e.ReviewId).HasColumnName("review_id");
        builder.Property(e => e.RescuerId).HasColumnName("rescuer_id");

        builder.Property(e => e.OldStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("old_status");

        builder.Property(e => e.NewStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("new_status");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Incident)
            .WithMany()
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AiReviewAuditLog_Incident_IncidentId");

        builder.HasOne(e => e.Review)
            .WithMany()
            .HasForeignKey(e => e.ReviewId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AiReviewAuditLog_AiInferenceReview_ReviewId");

        builder.HasOne(e => e.Rescuer)
            .WithMany()
            .HasForeignKey(e => e.RescuerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AiReviewAuditLog_User_RescuerId");
    }
}