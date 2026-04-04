using SFARS.Application.Dtos.Auth;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Models;

namespace SFARS.Application.Common
{
    public static class EmailTemplateFactory
    {
        public static (string Subject, string Body) BuildOtpEmail(
            AuthUserDto authUser,
            bool hasPassword,
            string otpCode,
            OtpType type)
        {
            if (type == OtpType.ResetPassword || type == OtpType.ChangePassword)
            {
                bool isChangePassword = type == OtpType.ChangePassword;
                bool isSetPassword = !isChangePassword && !hasPassword;
                string title = isChangePassword
                    ? "Đổi Mật Khẩu"
                    : (isSetPassword ? "Thiết Lập Mật Khẩu" : "Đặt Lại Mật Khẩu");
                string actionText = isChangePassword
                    ? "đổi mật khẩu"
                    : (isSetPassword ? "thiết lập mật khẩu" : "đặt lại mật khẩu");
                string warningActionText = isChangePassword
                    ? "đổi mật khẩu"
                    : (isSetPassword ? "thiết lập mật khẩu" : "tác vụ này");
                string warningSuffix = (isSetPassword || isChangePassword)
                    ? "."
                    : " và đổi mật khẩu ngay lập tức.";

                var subject = isChangePassword
                    ? "Change Password OTP for SFARS"
                    : (isSetPassword ? "Set Password OTP for SFARS" : "Password Reset OTP for SFARS");

                var body = $@"
                    <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                        <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                            <div style='background:#C0392B;color:#fff;padding:16px 24px;'>
                                <h2 style='margin:0;font-size:20px;'>⚠️ SFARS - Yêu Cầu {title}</h2>
                            </div>
                            <div style='padding:24px;color:#333;line-height:1.6;'>
                                <p>Xin chào <strong>{authUser.FirstName} {authUser.LastName}</strong>,</p>
                                <p>Chúng tôi nhận được yêu cầu <strong>{actionText}</strong> cho tài khoản của bạn. Đây là mã OTP:</p>
                                <div style='text-align:center;margin:20px 0;'>
                                    <span style='display:inline-block;background:#fdf2f2;color:#C0392B;
                                        font-size:28px;letter-spacing:6px;padding:12px 18px;border-radius:10px;border:2px solid #C0392B;'>
                                        {otpCode}
                                    </span>
                                </div>
                                <p>Mã có hiệu lực trong <strong>{OtpConstants.OtpExpirationMinutes} phút</strong>. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
                                <div style='background:#fdf2f2;border-left:4px solid #C0392B;padding:12px;margin:16px 0;border-radius:4px;'>
                                    <p style='margin:0;color:#C0392B;'><strong>⚠️ Cảnh báo bảo mật:</strong> Nếu bạn không yêu cầu {warningActionText}, vui lòng bỏ qua email này{warningSuffix}</p>
                                </div>
                                <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                            </div>
                        </div>
                    </div>";

                return (subject, body);
            }

            var signInSubject = "Your One-Time Password (OTP) for SFARS Sign-In";
            var signInBody = $@"
                <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                    <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                        <div style='background:#2C3E50;color:#fff;padding:16px 24px;'>
                            <h2 style='margin:0;font-size:20px;'>SFARS Verification</h2>
                        </div>
                        <div style='padding:24px;color:#333;line-height:1.6;'>
                            <p>Xin chào <strong>{authUser.FirstName} {authUser.LastName}</strong>,</p>
                            <p>Đây là mã OTP để đăng nhập:</p>
                            <div style='text-align:center;margin:20px 0;'>
                                <span style='display:inline-block;background:#f0f2f7;color:#2C3E50;
                                    font-size:28px;letter-spacing:6px;padding:12px 18px;border-radius:10px;'>
                                    {otpCode}
                                </span>
                            </div>
                            <p>Mã có hiệu lực trong <strong>{OtpConstants.OtpExpirationMinutes} phút</strong>. Vui lòng không chia sẻ mã này.</p>
                            <p style='margin-top:24px;'>Cảm ơn bạn đã sử dụng SFARS.</p>
                        </div>
                    </div>
                </div>";

            return (signInSubject, signInBody);
        }

        public static EmailMessageDto BuildRescuerRoleAssignedEmail(User user, string password)
        {
            var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name))).Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = "there";
            }

            return new EmailMessageDto
            {
                To = user.Email,
                Subject = "SFARS - Chào mừng bạn gia nhập đội cứu hộ (Rescuer Account)",
                Body = $@"
                    <div style='font-family: Arial, sans-serif; background:#f6f7fb; padding:24px;'>
                        <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;'>
                            <div style='background:#1F8B4C;color:#fff;padding:16px 24px;'>
                                <h2 style='margin:0;font-size:20px;'>SFARS - Tài Khoản Cứu Hộ</h2>
                            </div>
                            <div style='padding:24px;color:#333;line-height:1.6;'>
                                <p>Xin chào <strong>{fullName}</strong>,</p>
                                <p>Tài khoản của bạn đã được quản trị viên khởi tạo với vai trò <strong>Cứu hộ (Rescuer)</strong> trên hệ thống SFARS.</p>
                                
                                <div style='background:#f0f7f2; border-left:4px solid #1F8B4C; padding:16px; margin:20px 0;'>
                                    <p style='margin:0 0 10px 0;'><strong>Thông tin đăng nhập:</strong></p>
                                    <p style='margin:5px 0;'>Email: <strong>{user.Email}</strong></p>
                                    <p style='margin:5px 0;'>Mật khẩu tạm thời: <strong style='font-family:monospace; background:#e0e0e0; padding:2px 4px; border-radius:3px;'>{password}</strong></p>
                                </div>

                                <p style='color:#e67e22;'><strong>⚠️ Lưu ý bảo mật:</strong> Vì lý do an toàn, vui lòng đăng nhập và <strong>đổi mật khẩu ngay lập tức</strong> trong lần sử dụng đầu tiên.</p>
                                
                                <p style='margin-top:24px;'>Bây giờ bạn đã có thể đăng nhập và sử dụng các tính năng dành cho cứu hộ.</p>
                                <p>Nếu đây là một sự nhầm lẫn, vui lòng liên hệ với quản trị viên hệ thống.</p>
                            </div>
                        </div>
                    </div>"
            };
        }
    }
}