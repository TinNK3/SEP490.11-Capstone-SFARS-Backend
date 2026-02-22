using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations
{
    public class SnakeDtoValidator : AbstractValidator<SnakeDto>
    {
        public SnakeDtoValidator()
        {
            // Get language from static context (set by middleware/request)
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum =
                (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.CommonName)
                .NotEmpty()
                .WithMessage(isVi ? "Tên thường gọi không được để trống" : "Common name must not be empty")
                .MaximumLength(200)
                .WithMessage(isVi ? "Tên thường gọi không được vượt quá 200 ký tự" : "Common name must not exceed 200 characters");

            RuleFor(x => x.ScientificName)
                .NotEmpty()
                .WithMessage(isVi ? "Tên khoa học không được để trống" : "Scientific name must not be empty")
                .MaximumLength(200)
                .WithMessage(isVi ? "Tên khoa học không được vượt quá 200 ký tự" : "Scientific name must not exceed 200 characters");

            RuleFor(x => x.ToxicityLevel)
                .IsInEnum()
                .WithMessage(isVi ? "Mức độ độc tố không hợp lệ" : "Toxicity level is not valid");

            RuleFor(x => x.ToxinGroup)
                .IsInEnum()
                .WithMessage(isVi ? "Nhóm độc tố không hợp lệ" : "Toxin group is not valid");
        }
    }
}