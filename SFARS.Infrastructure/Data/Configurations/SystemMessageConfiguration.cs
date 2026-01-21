using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Type Configuration for SystemMessage entity
    /// </summary>
    public class SystemMessageConfiguration : IEntityTypeConfiguration<SystemMessage>
    {
        public void Configure(EntityTypeBuilder<SystemMessage> builder)
        {
            // Primary Key with named constraint
            builder.HasKey(e => e.MsgId).HasName("PK_SystemMessage_MsgId");

            // Table name - using singular form
            builder.ToTable("SystemMessage");

            // Properties configuration with explicit column names (snake_case)
            builder.Property(e => e.MsgId)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("msg_id");

            builder.Property(e => e.MsgContent)
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnName("msg_content");

            builder.Property(e => e.Vi)
                .HasMaxLength(1000)
                .HasColumnName("vietnamese_message");

            builder.Property(e => e.En)
                .HasMaxLength(1000)
                .HasColumnName("english_message");

            builder.Property(e => e.CreateBy)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("create_by");

            builder.Property(e => e.ModifiedBy)
                .HasMaxLength(100)
                .HasColumnName("modified_by");

            builder.Property(e => e.CreateDate)
                .HasColumnType("datetime")
                .HasColumnName("create_date");

            builder.Property(e => e.ModifiedDate)
                .HasColumnType("datetime")
                .HasColumnName("modified_date");

            // Index
            builder.HasIndex(e => e.MsgId)
                .IsUnique()
                .HasDatabaseName("IX_SystemMessage_MsgId");
        }
    }
}
