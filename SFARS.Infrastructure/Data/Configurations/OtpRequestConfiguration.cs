using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Type Configuration for OtpRequest entity
    /// </summary>
    public class OtpRequestConfiguration : IEntityTypeConfiguration<OtpRequest>
    {
        public void Configure(EntityTypeBuilder<OtpRequest> builder)
        {
            // Primary Key with named constraint
            builder.HasKey(e => e.Id).HasName("PK_OtpRequest_Id");

            // Table name - using singular form
            builder.ToTable("OtpRequest");

            // Properties configuration with explicit column names (snake_case)
            builder.Property(e => e.Id)
                .HasColumnName("id");

            builder.Property(e => e.UserId)
                .HasColumnName("user_id");

            builder.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("code");

            builder.Property(e => e.Purpose)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("purpose");

            builder.Property(e => e.ExpiredAt)
                .IsRequired()
                .HasColumnType("datetime")
                .HasColumnName("expired_at");

            builder.Property(e => e.IsUsed)
                .HasDefaultValue(false)
                .HasColumnName("is_used");

            builder.Property(e => e.AttemptCount)
                .HasDefaultValue(0)
                .HasColumnName("attempt_count");

            // BaseEntity properties
            builder.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            builder.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            builder.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            builder.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            // Indexes for better query performance
            builder.HasIndex(e => new { e.UserId, e.Purpose })
                .HasDatabaseName("IX_OtpRequest_UserId_Purpose");

            // Relationships with named constraints
            builder.HasOne(e => e.User)
                .WithMany(u => u.OtpRequests)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_OtpRequest_User_UserId");
        }
    }
}
