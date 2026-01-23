using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum PostType
    {
        [Description("Hướng dẫn")]
        Guide,
        [Description("Tin tức")]
        News,
        [Description("Thông báo")]
        Announcement
    }
}