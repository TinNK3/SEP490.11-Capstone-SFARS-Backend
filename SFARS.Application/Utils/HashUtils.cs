using System.Security.Cryptography;
using System.Text;

namespace SFARS.Application.Utils;

public static class HashUtils
{
    // ==========================================
    // 1. Password Hashing (BCrypt)
    // ==========================================

    /// <summary>
    /// Verify that the hash of given text matches the provided hash
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, storedHash);
    }

    /// <summary>
    /// Pre-hash a password with SHA384 then hash using the OpenBSD BCrypt scheme with a salt
    /// </summary>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 11);
    }

    // ==========================================
    // 2. Random Generator
    // ==========================================

    /// <summary>
    /// Generate a CRYPTOGRAPHICALLY SECURE random password
    /// </summary>
    public static string GenerateRandomPassword(int length = 12)
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string special = "@#$!"; // Add more special characters for better security if needed

        // Combine all character sets
        const string allChars = lower + upper + digits + special;

        // Use StringBuilder for memory efficiency
        var password = new StringBuilder();

        // Step 1: Ensure at least one character from each category (to pass validation)
        password.Append(GetRandomChar(upper));
        password.Append(GetRandomChar(lower));
        password.Append(GetRandomChar(digits));
        password.Append(GetRandomChar(special));

        // Step 2: Fill the remaining length with random characters
        int remainingLength = length - 4; // Subtract the 4 mandatory characters already added
        if (remainingLength > 0)
        {
            for (int i = 0; i < remainingLength; i++)
            {
                password.Append(GetRandomChar(allChars));
            }
        }

        // Step 3: Shuffle the characters randomly (Fisher-Yates shuffle)
        return ShuffleString(password.ToString());
    }

    /// <summary>
    /// Get a cryptographically secure random character from the given pool
    /// </summary>
    private static char GetRandomChar(string charPool)
    {
        // RandomNumberGenerator is more secure than the standard Random class
        return charPool[RandomNumberGenerator.GetInt32(charPool.Length)];
    }

    /// <summary>
    /// Shuffle a string using Fisher-Yates algorithm
    /// </summary>
    private static string ShuffleString(string str)
    {
        char[] array = str.ToCharArray();
        int n = array.Length;
        while (n > 1)
        {
            n--;
            int k = RandomNumberGenerator.GetInt32(n + 1);
            (array[k], array[n]) = (array[n], array[k]); // Swap using tuple syntax
        }
        return new string(array);
    }

    // ==========================================
    // 3. HMAC (API Signing)
    // ==========================================

    /// <summary>
    /// Compute HMAC-SHA256 hash of the given text using the specified key
    /// </summary>
    public static string HmacSha256(string text, string key)
    {
        // Use UTF8 encoding to support Unicode characters
        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);

        using var hash = new HMACSHA256(keyBytes);
        byte[] hashBytes = hash.ComputeHash(textBytes);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    // ==========================================
    // [NEW] 4. Data Integrity & Anonymization
    // Dùng cho: SFARS Specific Needs
    // ==========================================

    /// <summary>
    /// Fast SHA256 hashing (Not for password storage).
    /// /// Use cases:
    /// 1. Verify snake image integrity during upload/transfer.
    /// 2. Anonymize sensitive data (e.g., phone numbers) before sending to AI for training.
    /// </summary>
    /// <param name="rawData"></param>
    /// <returns></returns>
    public static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));

        // Convert to Hex string
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Compute the checksum of a file stream (Image/Video) to ensure data integrity.
    /// </summary>
    /// <param name="fileStream"></param>
    /// <returns></returns>
    public static string ComputeFileChecksum(Stream fileStream)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(fileStream);
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}