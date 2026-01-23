using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Type Configuration for RefreshToken entity
    /// </summary>
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            // Primary Key with named constraint
            builder.HasKey(e => e.Id).HasName("PK_RefreshToken_Id");

            // Table name - using singular form
            builder.ToTable("RefreshToken");

            // Properties configuration with explicit column names (snake_case)
            builder.Property(e => e.Id)
                .HasColumnName("id");

            builder.Property(e => e.RefreshTokenId)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("refresh_token_id");

            builder.Property(e => e.TokenId)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("token_id");

            builder.Property(e => e.CreateDate)
                .IsRequired()
                .HasColumnType("datetime")
                .HasColumnName("create_date");

            builder.Property(e => e.ExpiryDate)
                .IsRequired()
                .HasColumnType("datetime")
                .HasColumnName("expiry_date");

            builder.Property(e => e.UserId)
                .HasColumnName("user_id");

            builder.Property(e => e.RefreshCount)
                .HasDefaultValue(0)
                .HasColumnName("refresh_count");

            // Indexes for better query performance
            builder.HasIndex(e => e.RefreshTokenId)
                .HasDatabaseName("IX_RefreshToken_RefreshTokenId");

            builder.HasIndex(e => e.TokenId)
                .HasDatabaseName("IX_RefreshToken_TokenId");

            builder.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_RefreshToken_UserId");

            // Relationships with named constraints
            builder.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RefreshToken_UserId");
        }
    }
}