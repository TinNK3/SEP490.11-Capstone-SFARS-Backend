using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum MediaType
    {
        [Description("Ảnh rắn")]
        SnakePhoto = 0,

        [Description("Ảnh vết cắn")]
        BiteWoundPhoto = 1,

        [Description("Khác")]
        Other = 2
    }
}