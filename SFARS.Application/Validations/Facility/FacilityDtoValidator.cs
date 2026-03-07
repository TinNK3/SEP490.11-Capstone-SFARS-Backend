using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Facility
{
    /// <summary>
    /// Validator for FacilityDto.
    /// Validates name, coordinates (Vietnam bounds), phone format, and FacilityType.
    /// Used for both Create and Update operations.
    /// </summary>
    public class FacilityDtoValidator : AbstractValidator<FacilityDto>
    {
        public FacilityDtoValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage(isVi ? "Tên cơ sở không được để trống" : "Facility name is required")
                .MaximumLength(200)
                .WithMessage(isVi ? "Tên cơ sở không được vượt quá 200 ký tự" : "Facility name must not exceed 200 characters");

            RuleFor(x => x.FacilityType)
                .IsInEnum()
                .WithMessage(isVi ? "Loại cơ sở không hợp lệ" : "Invalid facility type");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(8.0, 23.5)
                .WithMessage(isVi
                    ? "Vĩ độ phải nằm trong phạm vi Việt Nam (8.0 – 23.5)"
                    : "Latitude must be within Vietnam bounds (8.0 – 23.5)");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(102.0, 110.0)
                .WithMessage(isVi
                    ? "Kinh độ phải nằm trong phạm vi Việt Nam (102.0 – 110.0)"
                    : "Longitude must be within Vietnam bounds (102.0 – 110.0)");

            RuleFor(x => x.Address)
                .MaximumLength(500)
                .When(x => !string.IsNullOrEmpty(x.Address))
                .WithMessage(isVi
                    ? "Địa chỉ không được vượt quá 500 ký tự"
                    : "Address must not exceed 500 characters");

            RuleFor(x => x.Province)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.Province))
                .WithMessage(isVi
                    ? "Tỉnh/thành phố không được vượt quá 100 ký tự"
                    : "Province must not exceed 100 characters");

            RuleFor(x => x.PhoneNumber)
                .Matches(@"^(\+84|0)[0-9]{9,10}$")
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
                .WithMessage(isVi
                    ? "Số điện thoại không đúng định dạng Việt Nam"
                    : "Phone number must be a valid Vietnamese format");

            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage(isVi
                    ? "Email không đúng định dạng"
                    : "Email is not a valid email address");

            RuleFor(x => x.Website)
                .MaximumLength(200)
                .When(x => !string.IsNullOrEmpty(x.Website))
                .WithMessage(isVi
                    ? "Website không được vượt quá 200 ký tự"
                    : "Website must not exceed 200 characters");

            RuleFor(x => x.CloseHours)
                .GreaterThan(x => x.OpenHours)
                .When(x => x.OpenHours.HasValue && x.CloseHours.HasValue)
                .WithMessage(isVi
                    ? "Giờ đóng cửa phải sau giờ mở cửa"
                    : "Close hours must be after open hours");

            RuleFor(x => x.Notes)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrEmpty(x.Notes))
                .WithMessage(isVi
                    ? "Ghi chú không được vượt quá 1000 ký tự"
                    : "Notes must not exceed 1000 characters");
        }
    }
}

