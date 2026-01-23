using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum PaymentGateway
    {
        [Description("Ví nội bộ")]
        InternalWallet,
        [Description("Momo")]
        Momo,
        [Description("ZaloPay")]
        ZaloPay,
        [Description("VNPay")]
        VNPay,
        [Description("Ngân hàng")]
        BankTransfer
    }
}