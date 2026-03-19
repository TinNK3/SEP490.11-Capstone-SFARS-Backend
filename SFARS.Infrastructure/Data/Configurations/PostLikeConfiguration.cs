using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("PostLike");

        // Composite PK
        builder.HasKey(e => new { e.PostId, e.UserId })
            .HasName("PK_PostLike");

        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.LikedAt).HasColumnName("liked_at");

        builder.HasIndex(e => e.PostId).HasDatabaseName("IX_PostLike_PostId");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_PostLike_UserId");

        builder.HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PostLike_ContentPost_PostId");

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PostLike_User_UserId");
    }
}
