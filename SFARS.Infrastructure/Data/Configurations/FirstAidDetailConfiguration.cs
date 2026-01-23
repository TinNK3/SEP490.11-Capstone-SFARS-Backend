using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class FirstAidDetailConfiguration : IEntityTypeConfiguration<FirstAidDetail>
{
    public void Configure(EntityTypeBuilder<FirstAidDetail> builder)
    {
        builder.ToTable("FirstAidDetail");
        builder.HasKey(e => e.Id).HasName("PK_FirstAidDetail_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SnakeId).HasColumnName("snake_id");

        builder.Property(e => e.StepOrder).HasColumnName("step_order");

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(e => e.ContentMarkdown)
            .HasColumnName("content_markdown");

        builder.Property(e => e.ImageUrl)
            .HasMaxLength(500)
            .HasColumnName("image_url");

        builder.Property(e => e.LanguageCode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasColumnName("language_code");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(d => d.Snake)
            .WithMany(s => s.FirstAidDetails)
            .HasForeignKey(d => d.SnakeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FirstAidDetail_Snake_SnakeId");
    }
}