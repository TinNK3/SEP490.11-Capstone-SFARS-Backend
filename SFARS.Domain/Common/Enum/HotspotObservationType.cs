using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum HotspotObservationType
    {
        [Description("Không rõ")]
        Unknown = 0,

        [Description("Thấy rắn")]
        SawSnake = 1,

        [Description("Bị cắn / vết cắn")]
        BiteOrWound = 2,

        [Description("Dấu vết khác")]
        OtherTrace = 3
    }
}