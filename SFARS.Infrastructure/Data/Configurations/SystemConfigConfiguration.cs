using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("SystemConfig");
        builder.HasKey(e => e.ConfigKey).HasName("PK_SystemConfig_ConfigKey");

        builder.Property(e => e.ConfigKey)
            .HasMaxLength(100)
            .HasColumnName("config_key");

        builder.Property(e => e.ConfigValue)
            .HasMaxLength(2000)
            .HasColumnName("config_value");

        builder.Property(e => e.Description)
            .HasMaxLength(500)
            .HasColumnName("description");
    }
}