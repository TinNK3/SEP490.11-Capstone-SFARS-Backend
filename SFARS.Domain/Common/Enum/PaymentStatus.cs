using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum PaymentStatus
    {
        [Description("Chưa thanh toán")]
        Pending,
        [Description("Đã hết hạn")]
        Expired,
        [Description("Đã thanh toán")]
        Paid,
        [Description("Đã hủy")]
        Cancelled
    }
}