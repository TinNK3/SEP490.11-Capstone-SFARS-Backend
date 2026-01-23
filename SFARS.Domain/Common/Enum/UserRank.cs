using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum UserRank
    {
        [Description("Đồng")]
        Bronze,
        [Description("Bạc")]
        Silver,
        [Description("Vàng")]
        Gold,
        [Description("Bạch kim")]
        Platinum,
        [Description("Kim cương")]
        Diamond
    }
}