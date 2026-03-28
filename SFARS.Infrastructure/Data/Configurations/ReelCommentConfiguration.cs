using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ReelCommentConfiguration : IEntityTypeConfiguration<ReelComment>
{
    public void Configure(EntityTypeBuilder<ReelComment> builder)
    {
        builder.HasKey(rc => rc.Id);

        builder.Property(rc => rc.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne(rc => rc.Reel)
            .WithMany(r => r.Comments)
            .HasForeignKey(rc => rc.ReelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rc => rc.User)
            .WithMany()
            .HasForeignKey(rc => rc.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(rc => rc.ParentComment)
            .WithMany(rc => rc.SubComments)
            .HasForeignKey(rc => rc.ParentCommentId)
            .OnDelete(DeleteBehavior.NoAction); // To prevent multiple cascade paths
    }
}
