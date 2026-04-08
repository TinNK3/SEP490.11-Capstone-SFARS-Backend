using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;
using SFARS.Domain.Common.Enum;

namespace SFARS.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        builder.HasKey(e => e.Id).HasName("PK_User_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("first_name");

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("last_name");

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("email");

        builder.Property(e => e.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasColumnName("password_hash");

        builder.Property(e => e.Avatar)
            .HasMaxLength(500)
            .HasColumnName("avatar");

        builder.Property(e => e.Address)
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(e => e.CurrentLocation)
            .HasColumnType("geography")
            .HasColumnName("current_location");

        builder.Property(e => e.LocationUpdatedAt)
            .HasColumnName("location_updated_at");

        builder.Property(e => e.LocationAccuracyMeters)
            .HasColumnName("location_accuracy_meters");

        builder.Property(e => e.LastLocationDeltaMeters)
            .HasColumnName("last_location_delta_meters");

        #region Update at: 4-02-2026 by Nguyen Khanh Tin
        builder.Property(e => e.EmailVerificationCode)
            .HasMaxLength(20)
            .HasColumnName("email_verification_code");
        #endregion

        builder.Property(e => e.Gender)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasColumnName("gender");

        builder.Property(e => e.Dob)
            .HasColumnName("dob");

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(e => e.IsOnline)
            .HasColumnName("is_online");

        builder.Property(e => e.LastActiveAt)
            .HasColumnName("last_active_at");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("IX_User_Email");
            
        builder.HasIndex(e => e.Phone)
            .HasDatabaseName("IX_User_Phone");

        builder.HasIndex(e => e.LocationUpdatedAt)
            .HasDatabaseName("IX_User_LocationUpdatedAt")
            .HasFilter("[location_updated_at] IS NOT NULL");
    }
}