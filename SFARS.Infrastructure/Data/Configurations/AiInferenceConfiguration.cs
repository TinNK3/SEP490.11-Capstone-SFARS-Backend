using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class AiInferenceConfiguration : IEntityTypeConfiguration<AiInference>
    {
        public void Configure(EntityTypeBuilder<AiInference> builder)
        {
            builder.ToTable("AiInference");
            builder.HasKey(e => e.Id).HasName("PK_AiInference_Id");

            builder.Property(e => e.Id).HasColumnName("id");

            builder.Property(e => e.IncidentId).HasColumnName("incident_id");
            builder.Property(e => e.IncidentMediaId).HasColumnName("incident_media_id");

            builder.Property(e => e.ModelName)
                .HasMaxLength(200)
                .HasColumnName("model_name");

            builder.Property(e => e.ModelVersion)
                .HasMaxLength(50)
                .HasColumnName("model_version");

            builder.Property(e => e.TopK)
                .HasColumnName("top_k");

            builder.Property(e => e.SelectedSnakeId).HasColumnName("selected_snake_id");
            builder.Property(e => e.SelectedConfidence).HasColumnName("selected_confidence");

            builder.Property(e => e.SelectedToxinGroup)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasColumnName("selected_toxin_group");

            builder.Property(e => e.DecisionRule)
                .HasMaxLength(200)
                .HasColumnName("decision_rule");

            builder.Property(e => e.CreatedAt).HasColumnName("created_at");
            builder.Property(e => e.CreatedBy).HasColumnName("created_by");
            builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            builder.HasIndex(e => e.IncidentId).HasDatabaseName("IX_AiInference_IncidentId");

            builder.HasOne(x => x.Incident)
                .WithMany(i => i.AiInferences)
                .HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AiInference_Incident_IncidentId");

            builder.HasOne(x => x.IncidentMedia)
                .WithMany()
                .HasForeignKey(x => x.IncidentMediaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_AiInference_IncidentMedia_IncidentMediaId");

            builder.HasOne(x => x.SelectedSnake)
                .WithMany()
                .HasForeignKey(x => x.SelectedSnakeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_AiInference_Snake_SelectedSnakeId");
        }
    }
}