using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Auth
{
    /// <summary>
    /// Validator for AuthenticateUserDto (used in sign-up and authentication)
    /// </summary>
    public class AuthUserDtoValidator : AbstractValidator<AuthUserDto>
    {
        public AuthUserDtoValidator()
        {
            // Get language from static context (set by middleware/request)
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

            // Password validation (for sign-up, password is required)
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage(isVi ? "Mật khẩu không được để trống" : "Password must not be empty")
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
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một ký tự đặc biệt" : "Password must contain at least one special character")
                .When(x => !string.IsNullOrEmpty(x.Password)); // Only validate if password is provided

            // FirstName validation
            RuleFor(x => x.FirstName)
                .NotEmpty()
                .WithMessage(isVi ? "Họ không được để trống" : "First name must not be empty")
                .MaximumLength(100)
                .WithMessage(isVi ? "Họ không được vượt quá 100 ký tự" : "First name must not exceed 100 characters");

            // LastName validation
            RuleFor(x => x.LastName)
                .NotEmpty()
                .WithMessage(isVi ? "Tên không được để trống" : "Last name must not be empty")
                .MaximumLength(100)
                .WithMessage(isVi ? "Tên không được vượt quá 100 ký tự" : "Last name must not exceed 100 characters");

            // Phone validation (optional)
            RuleFor(x => x.Phone)
                .Matches(@"^[\d\s\-\+\(\)]+$")
                .WithMessage(isVi ? "Số điện thoại không hợp lệ" : "Invalid phone number format")
                .When(x => !string.IsNullOrEmpty(x.Phone));
        }
    }
}