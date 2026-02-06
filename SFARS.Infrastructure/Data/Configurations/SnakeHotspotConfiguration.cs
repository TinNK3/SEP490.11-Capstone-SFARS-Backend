using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SnakeHotspotConfiguration : IEntityTypeConfiguration<SnakeHotspot>
{
    public void Configure(EntityTypeBuilder<SnakeHotspot> builder)
    {
        builder.ToTable("SnakeHotspot");
        builder.HasKey(e => e.Id).HasName("PK_SnakeHotspot_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReporterId).HasColumnName("reporter_id");
        builder.Property(e => e.SnakeId).HasColumnName("snake_id");

        builder.Property(e => e.Location)
            .IsRequired()
            .HasColumnType("geography")
            .HasColumnName("location");

        builder.Property(e => e.RiskLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("risk_level");

        builder.Property(e => e.Upvotes)
            .HasColumnName("upvotes");

        builder.Property(e => e.VerifiedByExpert)
            .HasColumnName("verified_by_expert");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(h => h.Reporter)
            .WithMany()
            .HasForeignKey(h => h.ReporterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SnakeHotspot_User_ReporterId");

        builder.HasOne(h => h.Snake)
            .WithMany()
            .HasForeignKey(h => h.SnakeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_SnakeHotspot_Snake_SnakeId");

        builder.Property(x => x.ObservationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("observation_type");

        builder.Property(x => x.ToxinGroup)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("toxin_group");

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_SnakeHotspot_ExpiresAt");
        builder.HasIndex(x => x.SnakeId).HasDatabaseName("IX_SnakeHotspot_SnakeId");
    }
}