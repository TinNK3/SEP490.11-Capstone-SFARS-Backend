using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ContentPostConfiguration : IEntityTypeConfiguration<ContentPost>
{
    public void Configure(EntityTypeBuilder<ContentPost> builder)
    {
        builder.ToTable("ContentPost");
        builder.HasKey(e => e.Id).HasName("PK_ContentPost_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AuthorId).HasColumnName("author_id");

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(e => e.Slug)
            .IsRequired()
            .HasMaxLength(250)
            .HasColumnName("slug");

        builder.Property(e => e.BodyContent)
            .HasColumnName("body_content");

        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("type");

        builder.Property(e => e.ThumbnailUrl)
            .HasMaxLength(500)
            .HasColumnName("thumbnail_url");

        builder.Property(e => e.IsPublished).HasColumnName("is_published");
        builder.Property(e => e.IsHiddenByAdmin).HasColumnName("is_hidden_by_admin").HasDefaultValue(false);
        builder.Property(e => e.AdminNote).HasColumnName("admin_note").HasMaxLength(1000);

        builder.Property(e => e.LikeCount).HasColumnName("like_count").HasDefaultValue(0);
        builder.Property(e => e.CommentCount).HasColumnName("comment_count").HasDefaultValue(0);
        builder.Property(e => e.ShareCount).HasColumnName("share_count").HasDefaultValue(0);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.Slug)
            .IsUnique()
            .HasDatabaseName("IX_ContentPost_Slug");

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ContentPost_User_AuthorId");

        builder.HasOne(p => p.SharedPost)
            .WithMany()
            .HasForeignKey(p => p.SharedPostId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.SharedReel)
            .WithMany()
            .HasForeignKey(p => p.SharedReelId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}