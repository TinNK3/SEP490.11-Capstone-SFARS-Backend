using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    public class IncidentChatConfiguration : IEntityTypeConfiguration<IncidentChat>
    {
        public void Configure(EntityTypeBuilder<IncidentChat> builder)
        {
            builder.ToTable("IncidentChats");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title)
                .HasMaxLength(200);

            builder.Property(x => x.LastMessageAt);

            // 1 Incident - 1 Chat (unique IncidentId)
            builder.HasIndex(x => x.IncidentId).IsUnique();

            builder.HasOne(x => x.Incident)
                .WithOne(i => i.Chat)
                .HasForeignKey<IncidentChat>(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Messages)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Incident)
                .WithOne(i => i.Chat)
                .HasForeignKey<IncidentChat>(x => x.IncidentId);
        }
    }
}