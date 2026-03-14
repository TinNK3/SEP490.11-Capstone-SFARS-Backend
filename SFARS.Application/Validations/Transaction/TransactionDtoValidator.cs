using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Transaction
{
    /// <summary>
    /// Validator for TransactionDto (validates fields used in creation: Amount, Description)
    /// </summary>
    public class TransactionDtoValidator : AbstractValidator<TransactionDto>
    {
        public TransactionDtoValidator()
        {
            // Get language from static context (set by middleware/request)
            var langContext = LanguageContext.CurrentLanguage ?? "vi";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            // Amount validation
            RuleFor(x => x.Amount)
                .NotEmpty()
                .WithMessage(isVi ? "Số tiền không được để trống" : "Amount must not be empty")
                .GreaterThanOrEqualTo(2_000)
                .WithMessage(isVi ? "Số tiền tối thiểu là 2,000 VND" : "Minimum amount is 2,000 VND")
                .LessThanOrEqualTo(100_000_000)
                .WithMessage(isVi ? "Số tiền tối đa là 100,000,000 VND" : "Maximum amount is 100,000,000 VND");

            // Description validation (optional, max 500 chars)
            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(isVi 
                    ? "Mô tả không được vượt quá 500 ký tự" 
                    : "Description must not exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));
        }
    }
}
