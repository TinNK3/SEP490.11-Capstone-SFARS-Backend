using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Auth
{
    /// <summary>
    /// Validator for ForgotPasswordDto
    /// </summary>
    public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public ForgotPasswordDtoValidator()
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
        }
    }
}
