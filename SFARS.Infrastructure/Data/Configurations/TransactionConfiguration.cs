using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFARS.Domain.Entities;

namespace SFARS.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transaction");
        builder.HasKey(e => e.Id).HasName("PK_Transaction_Id");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.MissionId).HasColumnName("mission_id");

        builder.Property(e => e.TransactionCode)
            .HasMaxLength(50)
            .HasColumnName("transaction_code");

        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)")
            .HasColumnName("amount");

        builder.Property(e => e.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(e => e.TransactionDate).HasColumnName("transaction_date");
        builder.Property(e => e.ExpiredAt).HasColumnName("expired_at");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelled_at");

        builder.Property(e => e.CancellationReason)
            .HasMaxLength(500)
            .HasColumnName("cancellation_reason");

        builder.Property(e => e.QrCode)
            .HasColumnName("qr_code");

        builder.Property(e => e.PaymentLinkId)
            .HasMaxLength(100)
            .HasColumnName("payment_link_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Transaction_User_UserId");
    }
}