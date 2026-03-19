using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SenderType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.ModelName)
            .HasMaxLength(100);

        builder.HasIndex(x => x.ChatSessionId);

        builder.HasOne(x => x.ChatSession)
            .WithMany(s => s.Messages)
            .HasForeignKey(x => x.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}