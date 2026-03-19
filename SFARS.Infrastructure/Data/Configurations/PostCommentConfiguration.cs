using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class PostCommentConfiguration : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> builder)
    {
        builder.ToTable("PostComment");
        builder.HasKey(e => e.Id).HasName("PK_PostComment_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.AuthorId).HasColumnName("author_id");
        builder.Property(e => e.ParentId).HasColumnName("parent_id");

        builder.Property(e => e.Content)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("content");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.PostId).HasDatabaseName("IX_PostComment_PostId");
        builder.HasIndex(e => e.ParentId).HasDatabaseName("IX_PostComment_ParentId");

        builder.HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PostComment_ContentPost_PostId");

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PostComment_User_AuthorId");

        // Self-referencing cho reply
        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PostComment_PostComment_ParentId");
    }
}
