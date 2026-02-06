using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incident");
        builder.HasKey(e => e.Id).HasName("PK_Incident_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.VictimId).HasColumnName("victim_id");
        builder.Property(e => e.SnakeId).HasColumnName("snake_id");

        builder.Property(e => e.CurrentAiInferenceId).HasColumnName("current_ai_inference_id");

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("code");

        builder.Property(e => e.Location)
            .IsRequired()
            .HasColumnType("geography")
            .HasColumnName("location");

        builder.Property(e => e.AddressString)
            .HasMaxLength(500)
            .HasColumnName("address_string");

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.CurrentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("current_status");

        builder.Property(e => e.PriorityLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("priority_level");

        builder.Property(e => e.AiPredictionResult)
            .HasColumnName("ai_prediction_result");

        builder.Property(e => e.AiConfidenceScore)
            .HasColumnName("ai_confidence_score");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("IX_Incident_Code");

        builder.HasOne(i => i.Victim)
            .WithMany()
            .HasForeignKey(i => i.VictimId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Incident_User_VictimId");

        builder.HasOne(i => i.Snake)
            .WithMany()
            .HasForeignKey(i => i.SnakeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Incident_Snake_SnakeId");

        builder.HasOne(i => i.CurrentAiInference)
            .WithMany()
            .HasForeignKey(i => i.CurrentAiInferenceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Incident_AiInference_CurrentAiInferenceId");

        builder.HasOne(i => i.Chat)
            .WithOne(c => c.Incident)
            .HasForeignKey<IncidentChat>(c => c.IncidentId);
    }
}