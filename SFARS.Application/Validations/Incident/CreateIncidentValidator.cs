using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Incident
{
    /// <summary>
    /// Validator for CreateIncident request
    /// </summary>
    public class CreateIncidentValidator : AbstractValidator<IncidentDto>
    {
        public CreateIncidentValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "vi";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            // Latitude validation (-90 to 90)
            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90)
                .WithMessage(isVi ? "Vĩ độ phải nằm trong khoảng -90 đến 90" : "Latitude must be between -90 and 90");

            // Longitude validation (-180 to 180)
            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .WithMessage(isVi ? "Kinh độ phải nằm trong khoảng -180 đến 180" : "Longitude must be between -180 and 180");

            // Description validation (optional, max 2000 chars)
            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithMessage(isVi ? "Mô tả không được vượt quá 2000 ký tự" : "Description must not exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            // AddressString validation (optional, max 500 chars)
            RuleFor(x => x.AddressString)
                .MaximumLength(500)
                .WithMessage(isVi ? "Địa chỉ không được vượt quá 500 ký tự" : "Address must not exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.AddressString));
        }
    }
}