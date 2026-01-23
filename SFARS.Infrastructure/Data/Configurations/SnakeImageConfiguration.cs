using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SnakeImageConfiguration : IEntityTypeConfiguration<SnakeImage>
{
    public void Configure(EntityTypeBuilder<SnakeImage> builder)
    {
        builder.ToTable("SnakeImage");
        builder.HasKey(e => e.Id).HasName("PK_SnakeImage_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SnakeId).HasColumnName("snake_id");

        builder.Property(e => e.ImageUrl)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("image_url");

        builder.Property(e => e.IsPrimary)
            .HasColumnName("is_primary");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(i => i.Snake)
            .WithMany(s => s.SnakeImages)
            .HasForeignKey(i => i.SnakeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_SnakeImage_Snake_SnakeId");
    }
}