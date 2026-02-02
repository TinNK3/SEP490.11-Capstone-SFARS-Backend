using FluentValidation;

namespace SFARS.Application.Validations.Auth
{
    public sealed class SignInWithGoogleValidator : AbstractValidator<string>
    {
        public SignInWithGoogleValidator()
        {
            RuleFor(x => x)
                .NotEmpty().WithMessage("Google Credential (ID Token) is required.")
                .MinimumLength(20).WithMessage("Google Credential looks invalid.");
        }
    }
}