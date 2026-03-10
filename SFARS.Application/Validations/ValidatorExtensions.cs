using FluentValidation;
using FluentValidation.Results;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Dtos.Faq;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.User;
using SFARS.Application.Validations.Auth;
using SFARS.Application.Validations.Facility;
using SFARS.Application.Validations.Faq;
using SFARS.Application.Validations.Incident;
using SFARS.Application.Validations.User;


namespace SFARS.Application.Validations
{
    public class ValidatorExtensions
    {
        // Define a dictionary to hold validators for each type.
        private static IValidator<T>? GetValidator<T>() where T : class
        {
            // Set current language before creating validator
            return typeof(T) switch
            {
                // Add cases for each DTO type and return the corresponding validator.
                { } when typeof(T) == typeof(SnakeDto) => (IValidator<T>)(object)new SnakeDtoValidator(),
                { } when typeof(T) == typeof(RefreshTokenDto) => (IValidator<T>)(object)new RefreshTokenDtoValidator(),
                { } when typeof(T) == typeof(AuthUserDto) => (IValidator<T>)(object)new AuthUserDtoValidator(),
                { } when typeof(T) == typeof(UserDto) => (IValidator<T>)(object)new UpdateProfileRequestValidator(),
                { } when typeof(T) == typeof(ForgotPasswordDto) => (IValidator<T>)(object)new ForgotPasswordDtoValidator(),
                { } when typeof(T) == typeof(ResetPasswordDto) => (IValidator<T>)(object)new ResetPasswordDtoValidator(),
                { } when typeof(T) == typeof(SendOtpDto) => (IValidator<T>)(object)new SendOtpDtoValidator(),
                { } when typeof(T) == typeof(VerifyOtpDto) => (IValidator<T>)(object)new VerifyOtpDtoValidator(),
                { } when typeof(T) == typeof(IncidentDto) => (IValidator<T>)(object)new CreateIncidentValidator(),
                { } when typeof(T) == typeof(FacilityDto) => (IValidator<T>)(object)new FacilityDtoValidator(),
                { } when typeof(T) == typeof(FaqDto) => (IValidator<T>)(object)new FaqDtoValidator(),
                _ => null
            };
        }
        public static async Task<ValidationResult?> ValidateAsync<T>(T dto) where T : class
        {
            // Create a new validator instance for the given type.
            var validator = GetValidator<T>();

            // Check if a validator exists for the given type.
            if (validator != null)
            {
                var result = await validator.ValidateAsync(dto);
                return !result.IsValid ? result : null;
            }

            // If no validator is found, throw an exception.
            throw new InvalidOperationException($"No validator found for type {typeof(T).Name}");
        }
    }
}
