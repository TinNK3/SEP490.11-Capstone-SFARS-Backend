using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum PointActivityType
    {
        [Description("Kiếm điểm")]
        Earn,
        [Description("Tiêu điểm")]
        Spend,
        [Description("Điều chỉnh")]
        Adjust
    }
}