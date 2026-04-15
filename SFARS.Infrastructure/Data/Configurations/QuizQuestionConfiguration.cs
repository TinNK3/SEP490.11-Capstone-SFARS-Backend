using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("QuizQuestion");
        builder.HasKey(e => e.Id).HasName("PK_QuizQuestion_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.QuizId).HasColumnName("quiz_id");

        builder.Property(e => e.Content)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("content");

        builder.Property(e => e.ImageUrl)
            .HasMaxLength(500)
            .HasColumnName("image_url");

        builder.Property(e => e.QuestionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("question_type");

        builder.Property(e => e.Explanation)
            .HasMaxLength(1000)
            .HasColumnName("explanation");

        builder.Property(e => e.Order)
            .HasColumnName("order");

        builder.HasOne(d => d.Quiz)
            .WithMany(p => p.QuizQuestions)
            .HasForeignKey(d => d.QuizId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuizQuestion_Quiz");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}
