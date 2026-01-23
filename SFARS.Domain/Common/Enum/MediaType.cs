using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum MediaType
    {
        [Description("Ảnh rắn")]
        SnakeImage,
        [Description("Ảnh vết cắn")]
        BiteImage,
        [Description("Ảnh xác minh")]
        VerifyImage,
        [Description("Ảnh triệu chứng")]
        SymptomPhoto,
        [Description("Video")]
        Video
    }
}