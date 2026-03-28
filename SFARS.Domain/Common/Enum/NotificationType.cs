using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum NotificationType
    {
        [Description("Hệ thống")]
        System = 0,
        [Description("Cứu hộ")]
        Mission = 1,
        [Description("Cảnh báo")]
        Alert = 2,
        [Description("Điều phối SOS")]
        SosDispatch = 3
    }
}