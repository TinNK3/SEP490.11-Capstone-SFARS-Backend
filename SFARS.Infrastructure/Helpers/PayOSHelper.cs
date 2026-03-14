using SFARS.Domain.Common.Constants;

namespace SFARS.Infrastructure.Helpers;

/// <summary>
/// Helper utilities for PayOS integration
/// </summary>
public static class PayOSHelper
{
    /// <summary>
    /// Generates a unique numeric order code for PayOS transactions.
    /// Combines timestamp with random suffix to ensure uniqueness.
    /// </summary>
    /// <returns>Long order code suitable for PayOS API</returns>
    public static long GenerateOrderCode()
    {
        // Combine timestamp + random suffix to ensure uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000L;
        var random = Random.Shared.Next(100, 999);
        return timestamp * 1000 + random;
    }

    /// <summary>
    /// Truncates description to PayOS maximum length (25 characters).
    /// </summary>
    /// <param name="description">Original description</param>
    /// <returns>Truncated description if longer than 25 chars</returns>
    public static string TruncateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return PaymentConstants.DefaultDescription;
            
        return description.Length > PaymentConstants.MaxDescriptionLength 
            ? description[..PaymentConstants.MaxDescriptionLength] 
            : description;
    }

    /// <summary>
    /// Validates payment amount meets PayOS requirements.
    /// Amount must be between min/max and divisible by 1,000 VND.
    /// </summary>
    /// <param name="amount">Amount to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateAmount(decimal amount, out string? errorMessage)
    {
        errorMessage = null;

        if (amount < PaymentConstants.MinimumAmount)
        {
            errorMessage = $"Số tiền tối thiểu là {PaymentConstants.MinimumAmount:N0} VND";
            return false;
        }

        if (amount > PaymentConstants.MaximumAmount)
        {
            errorMessage = $"Số tiền tối đa là {PaymentConstants.MaximumAmount:N0} VND";
            return false;
        }

        if (amount % PaymentConstants.AmountRoundingUnit != 0)
        {
            errorMessage = $"Số tiền phải chia hết cho {PaymentConstants.AmountRoundingUnit:N0} VND";
            return false;
        }

        return true;
    }
}
