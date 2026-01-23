using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class IncidentMediaConfiguration : IEntityTypeConfiguration<IncidentMedia>
{
    public void Configure(EntityTypeBuilder<IncidentMedia> builder)
    {
        builder.ToTable("IncidentMedia");
        builder.HasKey(e => e.Id).HasName("PK_IncidentMedia_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IncidentId).HasColumnName("incident_id");

        builder.Property(e => e.MediaUrl)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("media_url");

        builder.Property(e => e.MediaType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("media_type");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(m => m.Incident)
            .WithMany(i => i.Medias)
            .HasForeignKey(m => m.IncidentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_IncidentMedia_Incident_IncidentId");
    }
}