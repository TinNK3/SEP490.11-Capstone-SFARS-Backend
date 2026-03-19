using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class AiInferenceCandidateConfiguration : IEntityTypeConfiguration<AiInferenceCandidate>
    {
        public void Configure(EntityTypeBuilder<AiInferenceCandidate> builder)
        {
            builder.ToTable("AiInferenceCandidate");
            builder.HasKey(e => e.Id).HasName("PK_AiInferenceCandidate_Id");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.AiInferenceId).HasColumnName("ai_inference_id");
            builder.Property(e => e.Rank).HasColumnName("rank");
            builder.Property(e => e.SnakeId).HasColumnName("snake_id");
            builder.Property(e => e.Confidence).HasColumnName("confidence");

            builder.Property(e => e.CreatedAt).HasColumnName("created_at");
            builder.Property(e => e.CreatedBy).HasColumnName("created_by");
            builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            builder.HasIndex(e => new { e.AiInferenceId, e.Rank })
                .IsUnique()
                .HasDatabaseName("IX_AiInferenceCandidate_Inference_Rank");

            builder.HasOne(x => x.AiInference)
                .WithMany(i => i.Candidates)
                .HasForeignKey(x => x.AiInferenceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AiInferenceCandidate_AiInference_AiInferenceId");

            builder.HasOne(x => x.Snake)
                .WithMany()
                .HasForeignKey(x => x.SnakeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AiInferenceCandidate_Snake_SnakeId");
        }
    }
}