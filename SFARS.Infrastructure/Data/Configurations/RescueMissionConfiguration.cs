using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class RescueMissionConfiguration : IEntityTypeConfiguration<RescueMission>
{
    public void Configure(EntityTypeBuilder<RescueMission> builder)
    {
        builder.ToTable("RescueMission");
        builder.HasKey(e => e.Id).HasName("PK_RescueMission_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IncidentId).HasColumnName("incident_id");
        builder.Property(e => e.RescuerId).HasColumnName("rescuer_id");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.ArrivedAt).HasColumnName("arrived_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        builder.Property(e => e.RescuerNotes)
            .HasColumnName("rescuer_notes");

        builder.Property(e => e.PatientConditionAtHandover)
            .HasColumnName("patient_condition_at_handover");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(m => m.Incident)
            .WithMany(i => i.Missions)
            .HasForeignKey(m => m.IncidentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RescueMission_Incident_IncidentId");

        builder.HasOne(m => m.Rescuer)
            .WithMany()
            .HasForeignKey(m => m.RescuerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RescueMission_User_RescuerId");
    }
}