using FluentValidation;
using FluentValidation.Results;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Validations.Auth;

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