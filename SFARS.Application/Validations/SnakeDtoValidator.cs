using CloudinaryDotNet.Core;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using SFARS.Application.Dtos;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations
{
    public class SnakeDtoValidator : AbstractValidator<SnakeDto>
    {
        public SnakeDtoValidator(string langContext)
        {
            var langEnum =
                (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage(isVi ? "Tên rắn không được để trống" : "Snake name must not be empty")
                .MaximumLength(100)
                .WithMessage(isVi ? "Tên rắn không được vượt quá 100 ký tự" : "Snake name must not exceed 100 characters");
        }
    }
}