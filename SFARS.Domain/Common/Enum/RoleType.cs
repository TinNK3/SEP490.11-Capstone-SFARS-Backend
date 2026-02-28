using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    /// <summary>
    /// Assignable roles for user accounts.
    /// Mirrors the Role records in the database.
    /// </summary>
    public enum RoleType
    {
        [Description("Người dùng thường")]
        User,
        [Description("Nhân viên cứu hộ")]
        Rescuer,
        [Description("Quản trị viên")]
        Admin
    }
}
