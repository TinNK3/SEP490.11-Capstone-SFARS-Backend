using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class RetrainHistoryConfiguration : IEntityTypeConfiguration<RetrainHistory>
    {
        public void Configure(EntityTypeBuilder<RetrainHistory> builder)
        {
            builder.ToTable("RetrainHistory");
            builder.HasKey(e => e.Id).HasName("PK_RetrainHistory_Id");

            builder.Property(e => e.Id).HasColumnName("id");

            builder.Property(e => e.DatasetVersion)
                .HasMaxLength(100)
                .HasColumnName("dataset_version");

            builder.Property(e => e.ModelVersion)
                .HasMaxLength(100)
                .HasColumnName("model_version");

            builder.Property(e => e.TotalSamplesProcessed)
                .HasColumnName("total_samples_processed");

            builder.Property(e => e.OldAccuracy)
                .HasColumnName("old_accuracy");

            builder.Property(e => e.NewAccuracy)
                .HasColumnName("new_accuracy");

            builder.Property(e => e.IsPromoted)
                .HasColumnName("is_promoted");

            builder.Property(e => e.Status)
                .HasColumnName("status");

            builder.Property(e => e.ErrorMessage)
                .HasMaxLength(2000)
                .HasColumnName("error_message");

            builder.Property(e => e.StartedAt)
                .HasColumnName("started_at");

            builder.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            // BaseEntity properties
            builder.Property(e => e.CreatedAt).HasColumnName("created_at");
            builder.Property(e => e.CreatedBy).HasColumnName("created_by");
            builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        }
    }
}