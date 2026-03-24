using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Auth
{
    public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.CurrentPassword)
                .NotEmpty()
                .WithMessage(isVi ? "Mật khẩu hiện tại không được để trống" : "Current password must not be empty");

            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .WithMessage(isVi ? "Mật khẩu mới không được để trống" : "New password must not be empty")
                .MinimumLength(PasswordPolicyConstants.MinPasswordLength)
                .WithMessage(isVi
                    ? $"Mật khẩu phải có ít nhất {PasswordPolicyConstants.MinPasswordLength} ký tự"
                    : $"Password must be at least {PasswordPolicyConstants.MinPasswordLength} characters")
                .MaximumLength(PasswordPolicyConstants.MaxPasswordLength)
                .WithMessage(isVi
                    ? $"Mật khẩu không được vượt quá {PasswordPolicyConstants.MaxPasswordLength} ký tự"
                    : $"Password must not exceed {PasswordPolicyConstants.MaxPasswordLength} characters")
                .Matches(@"[A-Z]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ hoa" : "Password must contain at least one uppercase letter")
                .Matches(@"[a-z]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ thường" : "Password must contain at least one lowercase letter")
                .Matches(@"[0-9]")
                .WithMessage(isVi ? "Mật khẩu phải có ít nhất một chữ số" : "Password must contain at least one digit")
                .Must(ContainsAllowedSpecialCharacter)
                .WithMessage(isVi
                    ? $"Mật khẩu phải có ít nhất một ký tự đặc biệt ({PasswordPolicyConstants.AllowedSpecialCharacters})"
                    : $"Password must contain at least one special character ({PasswordPolicyConstants.AllowedSpecialCharacters})")
                .NotEqual(x => x.CurrentPassword)
                .WithMessage(isVi ? "Mật khẩu mới phải khác mật khẩu hiện tại" : "New password must be different from the current password")
                .Must(password => !ContainsConsecutiveIdenticalCharacters(password))
                .WithMessage(isVi
                    ? "Mật khẩu không được chứa quá 2 ký tự giống nhau liên tiếp"
                    : "Password cannot contain more than 2 consecutive identical characters");

            RuleFor(x => x.Otp)
                .NotEmpty()
                .WithMessage(isVi ? "Mã OTP không được để trống" : "OTP must not be empty")
                .Matches(@"^\d{6}$")
                .WithMessage(isVi ? "Mã OTP phải gồm 6 chữ số" : "OTP must be 6 digits");
        }

        private static bool ContainsAllowedSpecialCharacter(string password)
            => password.Any(c => PasswordPolicyConstants.AllowedSpecialCharacters.Contains(c));

        private static bool ContainsConsecutiveIdenticalCharacters(string password)
        {
            const int maxConsecutive = 2;
            int consecutiveCount = 1;

            for (int i = 1; i < password.Length; i++)
            {
                if (password[i] == password[i - 1])
                {
                    consecutiveCount++;
                    if (consecutiveCount > maxConsecutive)
                        return true;
                }
                else
                {
                    consecutiveCount = 1;
                }
            }

            return false;
        }
    }
}
