using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLog");
        builder.HasKey(e => e.Id).HasName("PK_NotificationLog_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(e => e.Message)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("message");

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("type");

        builder.Property(e => e.IsRead)
            .HasColumnName("is_read");

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at");

        builder.HasOne(n => n.User)
            .WithMany(u => u.NotificationLogs)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_NotificationLog_User_UserId");
    }
}