using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum SosSpamStatus
    {
        [Description("Cho phép")]
        Allowed,
        [Description("Cảnh báo nhẹ")]
        SoftWarning,
        [Description("Chặn cứng")]
        HardBlocked
    }
}