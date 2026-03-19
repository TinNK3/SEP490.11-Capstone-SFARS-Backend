using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class AiInferenceReviewConfiguration : IEntityTypeConfiguration<AiInferenceReview>
{
    public void Configure(EntityTypeBuilder<AiInferenceReview> builder)
    {
        builder.ToTable("AiInferenceReview");
        builder.HasKey(e => e.Id).HasName("PK_AiInferenceReview_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AiInferenceId).HasColumnName("ai_inference_id");
        builder.Property(e => e.IncidentId).HasColumnName("incident_id");
        builder.Property(e => e.ReviewerId).HasColumnName("reviewer_id");

        // Rule: 1 incident = 1 active review
        builder.HasIndex(e => e.IncidentId)
            .IsUnique()
            .HasFilter("[review_status] IN ('Pending', 'Deferred')")
            .HasDatabaseName("IX_AiInferenceReview_ActiveReview");

        builder.Property(e => e.ReviewStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("review_status");

        builder.Property(e => e.CorrectedSnakeId).HasColumnName("corrected_snake_id");

        builder.Property(e => e.CorrectedToxinGroup)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("corrected_toxin_group");

        builder.Property(e => e.UnableToAssessReasonChoice)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("unable_to_assess_reason_choice");

        builder.Property(e => e.Comment)
            .HasMaxLength(1000)
            .HasColumnName("comment");

        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.AiInference)
            .WithMany()
            .HasForeignKey(e => e.AiInferenceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AiInferenceReview_AiInference_AiInferenceId");

        builder.HasOne(e => e.Reviewer)
            .WithMany()
            .HasForeignKey(e => e.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AiInferenceReview_User_ReviewerId");

        builder.HasOne(e => e.Incident)
            .WithMany()
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AiInferenceReview_Incident_IncidentId");

        builder.HasOne(e => e.CorrectedSnake)
            .WithMany()
            .HasForeignKey(e => e.CorrectedSnakeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AiInferenceReview_Snake_CorrectedSnakeId");
    }
}