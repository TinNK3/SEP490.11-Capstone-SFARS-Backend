using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(e => e.Id).HasName("PK_Role_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.RoleName)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("role_name");

        builder.Property(e => e.Description)
            .HasMaxLength(200)
            .HasColumnName("description");
            
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.RoleName)
            .IsUnique()
            .HasDatabaseName("IX_Role_RoleName");
    }
}