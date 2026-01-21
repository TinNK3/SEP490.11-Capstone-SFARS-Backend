using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Type Configuration for Snake entity
    /// </summary>
    public class SnakeConfiguration : IEntityTypeConfiguration<Snake>
    {
        public void Configure(EntityTypeBuilder<Snake> builder)
        {
            // Primary Key with named constraint
            builder.HasKey(e => e.SnakeId).HasName("PK_Snake_SnakeId");

            // Table name - using singular form
            builder.ToTable("Snake");

            // Properties configuration with explicit column names (snake_case)
            builder.Property(e => e.SnakeId)
                .HasColumnName("snake_id");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");

            // Index for searching by name
            builder.HasIndex(e => e.Name)
                .HasDatabaseName("IX_Snake_Name");
        }
    }
}
