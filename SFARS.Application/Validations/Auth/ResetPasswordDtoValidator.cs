using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Auth
{
    /// <summary>
    /// Validator for ResetPasswordDto
    /// </summary>
    public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordDtoValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            // Email validation
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage(isVi ? "Email không được để trống" : "Email must not be empty")
                .EmailAddress()
                .WithMessage(isVi ? "Email không hợp lệ" : "Invalid email format")
                .MaximumLength(256)
                .WithMessage(isVi ? "Email không được vượt quá 256 ký tự" : "Email must not exceed 256 characters");

            // OTP validation
            RuleFor(x => x.Otp)
                .NotEmpty()
                .WithMessage(isVi ? "Mã OTP không được để trống" : "OTP must not be empty")
                .Length(6, 10)
                .WithMessage(isVi ? "Mã OTP phải từ 6-10 ký tự" : "OTP must be between 6-10 characters");

            // NewPassword validation
            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .WithMessage(isVi ? "Mật khẩu mới không được để trống" : "New password must not be empty")
                .MinimumLength(8)
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất 8 ký tự" : "Password must be at least 8 characters")
                .MaximumLength(128)
                .WithMessage(isVi ? "Mật khẩu không được vượt quá 128 ký tự" : "Password must not exceed 128 characters")
                .Matches(@"[A-Z]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ hoa" : "Password must contain at least one uppercase letter")
                .Matches(@"[a-z]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ thường" : "Password must contain at least one lowercase letter")
                .Matches(@"[0-9]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ số" : "Password must contain at least one digit")
                .Matches(@"[!@#$%^&*(),.?""':{}|<>]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một ký tự đặc biệt" : "Password must contain at least one special character");
        }
    }
}
