using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    /// <summary>
    /// Actions that an Admin can perform, logged in AdminAuditLog.
    /// </summary>
    public enum AdminAction
    {
        [Description("Tạo tài khoản")]
        CreateUser,

        [Description("Cập nhật trạng thái")]
        UpdateUserStatus,
    }
}
