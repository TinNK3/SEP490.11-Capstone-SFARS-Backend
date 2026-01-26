using FluentValidation;
using SFARS.Application.Dtos.Auth;

namespace SFARS.Application.Validations.Auth
{
    public class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
    {
        public RefreshTokenDtoValidator()
        {
            RuleFor(x => x.RefreshTokenId)
                .NotEmpty().WithMessage("Refresh token ID is required.");

            RuleFor(x => x.TokenId)
                .NotEmpty().WithMessage("Token ID is required.");

            RuleFor(x => x.ExpiryDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("Expiry date must be in the future.");

            RuleFor(x => x)
                .Must(x => x.UserId.HasValue || x.RescuerId.HasValue)
                .WithMessage("Either UserId or RescuerId must be provided.");
        }
    }
}
