using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Faq;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Faq
{
    public class FaqDtoValidator : AbstractValidator<FaqDto>
    {
        public FaqDtoValidator()
        {
            // Get language from static context (set by middleware/request)
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum =
                (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.Question)
                .NotEmpty()
                .WithMessage(isVi ? "Câu hỏi không được để trống" : "Question must not be empty")
                .MaximumLength(500)
                .WithMessage(isVi ? "Câu hỏi không được vượt quá 500 ký tự" : "Question must not exceed 500 characters");

            RuleFor(x => x.Answer)
                .NotEmpty()
                .WithMessage(isVi ? "Câu trả lời không được để trống" : "Answer must not be empty");

            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(0)
                .WithMessage(isVi ? "Thứ tự phải lớn hơn hoặc bằng 0" : "Order must be greater than or equal to 0");
        }
    }
}
