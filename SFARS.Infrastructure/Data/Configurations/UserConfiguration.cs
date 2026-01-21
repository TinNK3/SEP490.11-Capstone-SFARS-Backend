using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Type Configuration for User entity
    /// This class configures the database mapping for User table
    /// </summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Primary Key with named constraint
            builder.HasKey(e => e.UserId).HasName("PK_User_UserId");

            // Table name - using singular form
            builder.ToTable("User");

            // Properties configuration with explicit column names (snake_case)
            builder.Property(e => e.UserId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("user_id");

            builder.Property(e => e.RoleId)
                .HasColumnName("role_id");

            builder.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnName("email");

            builder.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("first_name");

            builder.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("last_name");

            builder.Property(e => e.PasswordHash)
                .HasMaxLength(500)
                .HasColumnName("password_hash");

            builder.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");

            builder.Property(e => e.Avatar)
                .HasMaxLength(2048)
                .IsUnicode(false)
                .HasColumnName("avatar");

            builder.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");

            builder.Property(e => e.Gender)
                .HasMaxLength(50)
                .HasColumnName("gender");

            builder.Property(e => e.Dob)
                .HasColumnType("datetime")
                .HasColumnName("dob");

            // Boolean properties with default values
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");

            builder.Property(e => e.TwoFactorEnabled)
                .HasDefaultValue(false)
                .HasColumnName("two_factor_enabled");

            builder.Property(e => e.PhoneNumberConfirmed)
                .HasDefaultValue(false)
                .HasColumnName("phone_number_confirmed");

            builder.Property(e => e.EmailConfirmed)
                .HasDefaultValue(false)
                .HasColumnName("email_confirmed");

            // Date properties
            builder.Property(e => e.CreateDate)
                .HasColumnType("datetime")
                .HasColumnName("create_date");

            builder.Property(e => e.ModifiedDate)
                .HasColumnType("datetime")
                .HasColumnName("modified_date");

            builder.Property(e => e.ModifiedBy)
                .HasMaxLength(155)
                .HasColumnName("modified_by");

            // Two-Factor Authentication properties
            builder.Property(e => e.TwoFactorSecretKey)
                .HasMaxLength(255)
                .HasColumnName("two_factor_secret_key");

            builder.Property(e => e.TwoFactorBackupCodes)
                .HasMaxLength(500)
                .HasColumnName("two_factor_backup_codes");

            // Phone verification properties
            builder.Property(e => e.PhoneVerificationCode)
                .HasMaxLength(20)
                .HasColumnName("phone_verification_code");

            builder.Property(e => e.PhoneVerificationExpiry)
                .HasColumnType("datetime")
                .HasColumnName("phone_verification_expiry");

            // Email verification property
            builder.Property(e => e.EmailVerificationCode)
                .HasMaxLength(20)
                .HasColumnName("email_verification_code");

            // Indexes for better query performance
            builder.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_User_Email");

            builder.HasIndex(e => e.Phone)
                .HasDatabaseName("IX_User_Phone");

            // Relationships with named constraints
            builder.HasMany(e => e.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RefreshToken_UserId");
        }
    }
}
