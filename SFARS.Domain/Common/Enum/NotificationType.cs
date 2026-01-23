using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum NotificationType
    {
        [Description("Hệ thống")]
        System,
        [Description("Cứu hộ")]
        Mission,
        [Description("Khuyến mãi")]
        Promotion,
        [Description("Cảnh báo")]
        Alert
    }
}