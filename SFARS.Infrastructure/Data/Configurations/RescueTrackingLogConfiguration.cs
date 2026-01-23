using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class RescueTrackingLogConfiguration : IEntityTypeConfiguration<RescueTrackingLog>
{
    public void Configure(EntityTypeBuilder<RescueTrackingLog> builder)
    {
        builder.ToTable("RescueTrackingLog");
        builder.HasKey(e => e.Id).HasName("PK_RescueTrackingLog_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.MissionId).HasColumnName("mission_id");
        builder.Property(e => e.RescuerId).HasColumnName("rescuer_id");

        builder.Property(e => e.Location)
            .IsRequired()
            .HasColumnType("geography")
            .HasColumnName("location");

        builder.Property(e => e.SpeedKMH).HasColumnName("speed_kmh");
        builder.Property(e => e.AccuracyMeters).HasColumnName("accuracy_meters");
        builder.Property(e => e.BatteryLevel).HasColumnName("battery_level");

        builder.Property(e => e.LoggedAt).HasColumnName("logged_at");

        builder.HasOne(l => l.Mission)
            .WithMany(m => m.TrackingLogs)
            .HasForeignKey(l => l.MissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RescueTrackingLog_RescueMission_MissionId");
    }
}