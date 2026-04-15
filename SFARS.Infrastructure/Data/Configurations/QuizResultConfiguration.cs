using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class QuizResultConfiguration : IEntityTypeConfiguration<QuizResult>
{
    public void Configure(EntityTypeBuilder<QuizResult> builder)
    {
        builder.ToTable("quiz_results");
        builder.HasKey(e => e.Id).HasName("pk_quiz_results_id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.QuizHistoryId).HasColumnName("quiz_history_id");
        builder.Property(e => e.QuestionId).HasColumnName("question_id");
        builder.Property(e => e.IsCorrect).HasColumnName("is_correct");
        builder.Property(e => e.SelectedOptionId).HasColumnName("selected_option_id");
        builder.Property(e => e.AnswerData).HasColumnName("answer_data").HasColumnType("text");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(d => d.QuizHistory)
            .WithMany(p => p.QuizResults)
            .HasForeignKey(d => d.QuizHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Question)
            .WithMany()
            .HasForeignKey(d => d.QuestionId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
