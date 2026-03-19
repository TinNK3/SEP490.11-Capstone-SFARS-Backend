using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class IncidentSymptomConfiguration : IEntityTypeConfiguration<IncidentSymptom>
    {
        public void Configure(EntityTypeBuilder<IncidentSymptom> builder)
        {
            builder.ToTable("IncidentSymptoms");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Notes)
                .HasMaxLength(2000);

            builder.Property(x => x.ReportedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Incident 1 - N Symptoms
            builder.HasOne(x => x.Incident)
                .WithMany(i => i.Symptoms)
                .HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional reporter
            builder.HasOne(x => x.Reporter)
                .WithMany()
                .HasForeignKey(x => x.ReportedBy)
                .OnDelete(DeleteBehavior.NoAction);

            // Indexes
            builder.HasIndex(x => x.IncidentId);
            builder.HasIndex(x => x.ReportedAt);
        }
    }
}