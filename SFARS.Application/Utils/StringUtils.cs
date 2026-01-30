using System.Globalization;
using System.Text.RegularExpressions;

namespace SFARS.Application.Utils
{
    // Summary:
    //		Provide utility procedures to handle any logic related to String datatype
    public static class StringUtils
    {
        /// <summary>
        /// Generates a random alphanumeric code of the specified length.
        /// </summary>
        /// <param name="length">The length of the code to generate. Defaults to 6.</param>
        /// <returns>A randomly generated alphanumeric string.</returns>
        public static string GenerateUniqueCode(int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)])
                .ToArray());
        }

        /// <summary>
        /// Removes extra spaces from the input string and converts it to title case using the current culture.
        /// </summary>
        /// <param name="input">The string to clean and convert to title case.</param>
        /// <returns>A cleaned, title-cased version of the input string, or an empty string if the input is null or whitespace.</returns>
        public static string CleanAndTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Remove extra spaces: " a b " -> "a b"
            string clean = Regex.Replace(input.Trim(), @"\s+", " ");

            // Capitalize the first letter (Title Case)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean.ToLower());
        }

        /// <summary>
        /// Replaces placeholders in the input string, such as <0>, <1>, etc., with corresponding values from the args
        /// array.
        /// </summary>
        /// <param name="input">The string containing placeholders to be replaced.</param>
        /// <param name="args">An array of strings to substitute for the placeholders in the input.</param>
        /// <returns>A formatted string with placeholders replaced by argument values, or the original string if no arguments are
        /// provided.</returns>
        public static string Format(string input, params string[]? args)
        {
            if (string.IsNullOrEmpty(input))
                return null!;

            if (args == null || args.Length == 0)
                return input; // Return original string if no args provided.

            for (int i = 0; i < args.Length; i++)
            {
                input = input.Replace($"{{{i}}}", args[i]);
            }

            return input;
        }

        /// <summary>
        /// Converts the specified string to camel case, changing the initial uppercase letters to lowercase as
        /// appropriate.
        /// </summary>
        /// <param name="s">The string to convert to camel case.</param>
        /// <returns>A camel case version of the input string.</returns>
        public static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
            {
                return s;
            }

            var chars = s.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                var hasNext = (i + 1 < chars.Length);
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    break;
                }

                chars[i] = char.ToLower(chars[i], CultureInfo.InvariantCulture);
            }

            return new string(chars);
        }
    }
}
