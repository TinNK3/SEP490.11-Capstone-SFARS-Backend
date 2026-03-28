using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ReelConfiguration : IEntityTypeConfiguration<Reel>
{
    public void Configure(EntityTypeBuilder<Reel> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Caption)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(r => r.VideoUrl)
            .IsRequired();

        builder.Property(r => r.CloudinaryPublicId)
            .IsRequired();

        // Relationships
        builder.HasOne(r => r.User)
            .WithMany() // Assuming User doesn't need to know all Reels
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
