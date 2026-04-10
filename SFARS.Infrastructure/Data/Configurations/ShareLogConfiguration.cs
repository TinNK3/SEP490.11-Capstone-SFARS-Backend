using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ShareLogConfiguration : IEntityTypeConfiguration<ShareLog>
{
    public void Configure(EntityTypeBuilder<ShareLog> builder)
    {
        builder.ToTable("Shares");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Post)
            .WithMany()
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Reel)
            .WithMany()
            .HasForeignKey(x => x.ReelId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
