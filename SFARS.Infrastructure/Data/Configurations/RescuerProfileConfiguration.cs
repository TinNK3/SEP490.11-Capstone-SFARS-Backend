using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class RescuerProfileConfiguration : IEntityTypeConfiguration<RescuerProfile>
{
    public void Configure(EntityTypeBuilder<RescuerProfile> builder)
    {
        builder.ToTable("RescuerProfile");
        builder.HasKey(e => e.UserId).HasName("PK_RescuerProfile_UserId");

        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.Property(e => e.ExperienceYears).HasColumnName("experience_years");
        builder.Property(e => e.VehicleType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("vehicle_type");
        builder.Property(e => e.LicensePlate).HasMaxLength(20).HasColumnName("license_plate");
        builder.Property(e => e.CoverageRadiusKM).HasColumnName("coverage_radius_km");
        builder.Property(e => e.IsVerified).HasColumnName("is_verified");
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by");

        builder.Property(x => x.IsAvailable)
            .IsRequired()
            .HasColumnName("is_available");

        builder.Property(x => x.AvailableUpdatedAt)
            .HasColumnName("available_updated_at");

        builder.HasOne(p => p.User)
            .WithOne(u => u.RescuerProfile)
            .HasForeignKey<RescuerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RescuerProfile_User_UserId");
    }
}