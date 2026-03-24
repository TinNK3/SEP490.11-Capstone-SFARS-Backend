namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Password policy constants for validation
/// </summary>
public static class PasswordPolicyConstants
{
    /// <summary>
    /// Minimum password length
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// Maximum password length
    /// </summary>
    public const int MaxPasswordLength = 255;

    /// <summary>
    /// Password must contain at least one uppercase letter
    /// </summary>
    public static bool RequireUppercase = true;

    /// <summary>
    /// Password must contain at least one lowercase letter
    /// </summary>
    public static bool RequireLowercase = true;

    /// <summary>
    /// Password must contain at least one digit
    /// </summary>
    public static bool RequireDigit = true;

    /// <summary>
    /// Password must contain at least one special character
    /// </summary>
    public static bool RequireSpecialCharacter = true;

    /// <summary>
    /// Allowed special characters
    /// </summary>
    public const string AllowedSpecialCharacters = "!@#$%^&*()_+-=[]{}|;:',.<>?/`~";
}
