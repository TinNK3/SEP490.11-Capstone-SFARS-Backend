using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Auth
{
    /// <summary>
    /// Validator for SendOtpDto
    /// </summary>
    public class SendOtpDtoValidator : AbstractValidator<SendOtpDto>
    {
        public SendOtpDtoValidator()
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

            // Purpose validation
            RuleFor(x => x.Purpose)
                .NotEmpty()
                .WithMessage(isVi ? "Mục đích OTP không được để trống" : "OTP purpose must not be empty")
                .Must(BeAValidPurpose)
                .WithMessage(isVi ? "Mục đích OTP không hợp lệ. Chỉ chấp nhận: SIGN_IN, RESET_PASSWORD" : "Invalid OTP purpose. Accepted values: SIGN_IN, RESET_PASSWORD");
        }

        private bool BeAValidPurpose(string purpose)
        {
            return purpose == "SIGN_IN" || purpose == "RESET_PASSWORD";
        }
    }
}
