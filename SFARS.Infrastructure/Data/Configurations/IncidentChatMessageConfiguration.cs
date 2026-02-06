using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class IncidentChatMessageConfiguration : IEntityTypeConfiguration<IncidentChatMessage>
    {
        public void Configure(EntityTypeBuilder<IncidentChatMessage> builder)
        {
            builder.ToTable("IncidentChatMessages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(4000); // đủ chat + tránh nvarchar(max) nếu bạn muốn tối ưu index/scan

            builder.Property(x => x.ModelName)
                .HasMaxLength(200);

            builder.Property(x => x.ModelVersion)
                .HasMaxLength(50);

            builder.Property(x => x.MetadataJson)
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.SenderType)
                .HasConversion<string>();

            builder.HasIndex(x => new { x.ChatId, x.CreatedAt });

            builder.HasOne(x => x.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.AiInference)
                .WithMany()
                .HasForeignKey(x => x.AiInferenceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(x => x.Medias)
                .WithOne(m => m.Message)
                .HasForeignKey(m => m.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}