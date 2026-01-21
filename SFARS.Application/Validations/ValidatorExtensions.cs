using FluentValidation;
using FluentValidation.Results;
using SFARS.Application.Common;
using SFARS.Application.Dtos;

namespace SFARS.Application.Validations
{
    public class ValidatorExtensions
    {
        // Define a dictionary to hold validators for each type.
        private static IValidator<T>? GetValidator<T>(string language) where T : class
        {
            return typeof(T) switch
            {
                // Add cases for each DTO type and return the corresponding validator.
                { } when typeof(T) == typeof(SnakeDto) => (IValidator<T>)new SnakeDtoValidator(language),
                _ => null
            };
        }
        public static async Task<ValidationResult?> ValidateAsync<T>(T dto) where T : class
        {
            // Retrieve current language
            var currentLanguage = LanguageContext.CurrentLanguage;

            // Create a new validator instance for the given type, passing the language dynamically.
            var validator = GetValidator<T>(currentLanguage);

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