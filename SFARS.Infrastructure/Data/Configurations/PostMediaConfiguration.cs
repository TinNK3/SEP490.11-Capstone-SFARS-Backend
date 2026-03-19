using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("PostMedia");
        builder.HasKey(e => e.Id).HasName("PK_PostMedia_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PostId).HasColumnName("post_id");

        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("url");

        builder.Property(e => e.ContentType)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("content_type");

        builder.Property(e => e.Order)
            .HasColumnName("order")
            .HasDefaultValue(0);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.PostId).HasDatabaseName("IX_PostMedia_PostId");

        builder.HasOne(m => m.Post)
            .WithMany(p => p.Medias)
            .HasForeignKey(m => m.PostId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PostMedia_ContentPost_PostId");
    }
}
