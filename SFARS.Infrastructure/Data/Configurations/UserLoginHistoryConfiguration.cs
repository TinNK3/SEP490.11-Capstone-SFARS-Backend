using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class UserLoginHistoryConfiguration : IEntityTypeConfiguration<UserLoginHistory>
{
    public void Configure(EntityTypeBuilder<UserLoginHistory> builder)
    {
        builder.ToTable("UserLoginHistory");
        builder.HasKey(e => e.Id).HasName("PK_UserLoginHistory_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.Property(e => e.LoginAt)
            .HasColumnName("login_at");

        builder.Property(e => e.IPAddress)
            .HasMaxLength(50)
            .HasColumnName("ip_address");

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500)
            .HasColumnName("user_agent");

        builder.HasOne(h => h.User)
            .WithMany(u => u.LoginHistories)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserLoginHistory_User_UserId");
    }
}