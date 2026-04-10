using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SnakeConfiguration : IEntityTypeConfiguration<Snake>
{
    public void Configure(EntityTypeBuilder<Snake> builder)
    {
        builder.ToTable("Snake");
        builder.HasKey(e => e.Id).HasName("PK_Snake_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ScientificName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("scientific_name");

        builder.HasIndex(e => e.ScientificName)
            .IsUnique()
            .HasDatabaseName("IX_Snake_ScientificName");

        builder.Property(e => e.CommonName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("common_name");

        builder.Property(e => e.ToxicityLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("toxicity_level");

        builder.Property(e => e.ToxinGroup)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("toxin_group");

        builder.Property(e => e.Description)
             .HasColumnName("description");

        builder.Property(e => e.KeyIdentifiers)
             .HasColumnName("key_identifiers");

        builder.Property(e => e.TypicalSymptoms)
             .HasColumnName("typical_symptoms");

        builder.Property(e => e.Habitat)
             .HasColumnName("habitat");

        builder.Property(e => e.DistributionNote)
             .HasColumnName("distribution_note");

        builder.Property(e => e.Note)
             .HasColumnName("note");

        builder.Property(e => e.IsActive)
             .HasColumnName("is_active");

        builder.Property(e => e.EmbeddingJson)
             .HasColumnName("embedding_json");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}