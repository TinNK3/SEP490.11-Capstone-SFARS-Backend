using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum TransactionStatus
    {
        [Description("Chờ xử lý")]
        Pending,
        [Description("Thành công")]
        Success,
        [Description("Thất bại")]
        Failed,
        [Description("Đã hủy")]
        Cancelled
    }
}