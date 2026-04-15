using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class QuizOptionConfiguration : IEntityTypeConfiguration<QuizOption>
{
    public void Configure(EntityTypeBuilder<QuizOption> builder)
    {
        builder.ToTable("QuizOption");
        builder.HasKey(e => e.Id).HasName("PK_QuizOption_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.QuestionId).HasColumnName("question_id");

        builder.Property(e => e.Content)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("content");

        builder.Property(e => e.IsCorrect)
            .HasColumnName("is_correct");

        builder.Property(e => e.StepOrder)
            .HasColumnName("step_order");

        builder.Property(e => e.PenaltyNote)
            .HasMaxLength(500)
            .HasColumnName("penalty_note");

        builder.HasOne(d => d.Question)
            .WithMany(p => p.Options)
            .HasForeignKey(d => d.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuizOption_QuizQuestion");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}
