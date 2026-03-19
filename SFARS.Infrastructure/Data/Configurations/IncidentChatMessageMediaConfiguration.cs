using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class IncidentChatMessageMediaConfiguration : IEntityTypeConfiguration<IncidentChatMessageMedia>
    {
        public void Configure(EntityTypeBuilder<IncidentChatMessageMedia> builder)
        {
            builder.ToTable("IncidentChatMessageMedias");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.MediaUrl)
                .HasMaxLength(2048);

            builder.HasIndex(x => x.MessageId);

            builder.HasOne(x => x.Message)
                .WithMany(m => m.Medias)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.IncidentMedia)
                .WithMany()
                .HasForeignKey(x => x.IncidentMediaId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}