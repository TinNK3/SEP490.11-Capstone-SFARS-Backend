using System.Globalization;
using System.Text.RegularExpressions;

namespace SFARS.Application.Utils
{
    // Summary:
    //		Provide utility procedures to handle any logic related to String datatype
    public static class StringUtils
    {
        private static readonly Random _rnd = new Random();

        /// <summary>
        // Clean up names (Snake name, Victim name) entered carelessly by the user in a panic.
        /// VD: "  rắn   hổ mang  " -> "Rắn Hổ Mang"
        /// </summary>
        public static string CleanAndTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Remove extra spaces: " a b " -> "a b"
            string clean = Regex.Replace(input.Trim(), @"\s+", " ");

            // Capitalize the first letter (Title Case)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean.ToLower());
        }

        // Formats a string by replacing placeholders like <0>, <1>, etc., with the provided arguments.
        public static string Format(string input, params string[]? args)
        {
            if (string.IsNullOrEmpty(input))
                return null!;

            if (args == null || args.Length == 0)
                return input; // Return original string if no args provided.

            for (int i = 0; i < args.Length; i++)
            {
                input = input.Replace($"<{i}>", args[i]);
            }

            return input;
        }
    }
}
