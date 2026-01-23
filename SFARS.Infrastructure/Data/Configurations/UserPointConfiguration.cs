using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class UserPointConfiguration : IEntityTypeConfiguration<UserPoint>
{
    public void Configure(EntityTypeBuilder<UserPoint> builder)
    {
        builder.ToTable("UserPoint");
        builder.HasKey(e => e.UserId).HasName("PK_UserPoint_UserId");

        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.Property(e => e.CurrentPoints).HasColumnName("current_points");
        builder.Property(e => e.LifetimePoints).HasColumnName("lifetime_points");

        builder.Property(e => e.CurrentRank)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("current_rank");

        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.User)
            .WithOne(u => u.UserPoint)
            .HasForeignKey<UserPoint>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserPoint_User_UserId");
    }
}