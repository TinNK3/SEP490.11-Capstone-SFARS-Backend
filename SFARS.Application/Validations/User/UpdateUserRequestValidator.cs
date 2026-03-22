using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.User;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.User
{
    /// <summary>
    /// Validator for admin updating user profile via UserDto.
    /// This validator is used for both direct profile updates and admin-driven updates.
    /// Note: We validate the DTO after mapping from request, not the request itself,
    /// to maintain clean architecture (Application layer doesn't depend on API layer).
    /// </summary>
    public class UpdateUserRequestValidator : AbstractValidator<UserDto>
    {
        public UpdateUserRequestValidator()
        {
            // Get language from static context
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

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

            // Address validation (optional)
            RuleFor(x => x.Address)
                .MaximumLength(500)
                .WithMessage(isVi ? "Địa chỉ không được vượt quá 500 ký tự" : "Address must not exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Address));

            // Avatar URL validation (optional)
            RuleFor(x => x.Avatar)
                .MaximumLength(2048)
                .WithMessage(isVi ? "URL Avatar không được vượt quá 2048 ký tự" : "Avatar URL must not exceed 2048 characters")
                .When(x => !string.IsNullOrEmpty(x.Avatar));

            // Dob validation (optional - must be in the past)
            RuleFor(x => x.Dob)
                .LessThan(DateTime.Today)
                .WithMessage(isVi ? "Ngày sinh phải là ngày trong quá khứ" : "Date of birth must be in the past")
                .When(x => x.Dob.HasValue);
        }
    }
}
