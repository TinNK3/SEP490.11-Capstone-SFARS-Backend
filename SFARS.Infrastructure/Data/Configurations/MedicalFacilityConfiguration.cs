using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class MedicalFacilityConfiguration : IEntityTypeConfiguration<MedicalFacility>
{
    public void Configure(EntityTypeBuilder<MedicalFacility> builder)
    {
        builder.ToTable("MedicalFacility");
        builder.HasKey(e => e.Id).HasName("PK_MedicalFacility_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("type");

        builder.Property(e => e.Address)
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(e => e.Location)
            .IsRequired()
            .HasColumnType("geography")
            .HasColumnName("location");

        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(20)
            .HasColumnName("phone_number");

        builder.Property(e => e.OperatingHours)
            .HasMaxLength(100)
            .HasColumnName("operating_hours");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}