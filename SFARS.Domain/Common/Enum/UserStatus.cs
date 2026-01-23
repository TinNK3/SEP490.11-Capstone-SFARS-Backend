using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum UserStatus
    {
        [Description("Hoạt động")]
        Active,
        [Description("Vô hiệu hóa")]
        Inactive,
        [Description("Bị khóa")]
        Banned,
        [Description("Đã xóa")]
        Deleted
    }
}