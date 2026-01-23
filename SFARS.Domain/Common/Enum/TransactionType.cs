using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum TransactionType
    {
        [Description("Nạp tiền")]
        Deposit,
        [Description("Thanh toán")]
        Payment,
        [Description("Hoàn tiền")]
        Refund
    }
}