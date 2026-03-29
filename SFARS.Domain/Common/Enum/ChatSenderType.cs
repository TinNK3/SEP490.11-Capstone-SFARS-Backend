using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum ChatSenderType
    {
        [Description("Người dùng")]
        User,
        [Description("AI")]
        AI,
        [Description("Hệ thống")]
        System
    }
}