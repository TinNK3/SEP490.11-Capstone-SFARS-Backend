using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Review");
        builder.HasKey(e => e.Id).HasName("PK_Review_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.MissionId).HasColumnName("mission_id");
        builder.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
        builder.Property(e => e.TargetId).HasColumnName("target_id");

        builder.Property(e => e.Rating)
            .IsRequired()
            .HasColumnName("rating");

        builder.Property(e => e.Comment)
            .HasColumnName("comment");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(r => r.Mission)
            .WithOne(m => m.Review)
            .HasForeignKey<Review>(r => r.MissionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Review_RescueMission_MissionId");

        builder.HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Review_User_ReviewerId");

        builder.HasOne(r => r.Target)
            .WithMany()
            .HasForeignKey(r => r.TargetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Review_User_TargetId");
    }
}