using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class SystemMessageConfiguration : IEntityTypeConfiguration<SystemMessage>
{
    public void Configure(EntityTypeBuilder<SystemMessage> builder)
    {
        builder.ToTable("SystemMessage");
        builder.HasKey(e => e.Id).HasName("PK_SystemMessage_Id");

        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.MsgId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("msg_id");

        builder.Property(e => e.MsgContent)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("msg_content");

        builder.Property(e => e.Vi)
            .HasMaxLength(2000)
            .HasColumnName("vi");

        builder.Property(e => e.En)
            .HasMaxLength(2000)
            .HasColumnName("en");
            
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.MsgId)
            .IsUnique()
            .HasDatabaseName("IX_SystemMessage_MsgId");
    }
}