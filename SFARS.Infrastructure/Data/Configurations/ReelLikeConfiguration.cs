using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ReelLikeConfiguration : IEntityTypeConfiguration<ReelLike>
{
    public void Configure(EntityTypeBuilder<ReelLike> builder)
    {
        builder.HasKey(rl => new { rl.ReelId, rl.UserId });

        builder.HasOne(rl => rl.Reel)
            .WithMany(r => r.Likes)
            .HasForeignKey(rl => rl.ReelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rl => rl.User)
            .WithMany()
            .HasForeignKey(rl => rl.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
