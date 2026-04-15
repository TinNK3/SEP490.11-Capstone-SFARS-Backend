using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class QuizHistoryConfiguration : IEntityTypeConfiguration<QuizHistory>
{
    public void Configure(EntityTypeBuilder<QuizHistory> builder)
    {
        builder.ToTable("quiz_histories");
        builder.HasKey(e => e.Id).HasName("pk_quiz_histories_id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.QuizId).HasColumnName("quiz_id");
        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.TotalQuestions).HasColumnName("total_questions");
        builder.Property(e => e.PointsEarned).HasColumnName("points_earned");
        builder.Property(e => e.IsPassed).HasColumnName("is_passed");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Quiz)
            .WithMany()
            .HasForeignKey(d => d.QuizId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
