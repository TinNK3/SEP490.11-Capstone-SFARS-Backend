using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum OtpType
    {
        [Description("Đăng nhập")]
        SignIn,
        [Description("Đặt lại mật khẩu")]
        ResetPassword,
        [Description("Đổi mật khẩu")]
        ChangePassword
    }
}
