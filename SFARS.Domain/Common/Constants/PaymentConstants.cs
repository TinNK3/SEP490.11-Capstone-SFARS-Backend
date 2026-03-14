namespace SFARS.Domain.Common.Constants;

/// <summary>
/// Constants for payment and transaction operations
/// </summary>
public static class PaymentConstants
{
    /// <summary>
    /// Default description for donations when none provided
    /// </summary>
    public const string DefaultDescription = "UNG HO SFARS";
    
    /// <summary>
    /// Display description for PayOS payment items
    /// </summary>
    public const string PaymentItemDescription = "Ủng hộ SFARS";
    
    /// <summary>
    /// Minimum allowed payment amount in VND
    /// </summary>
    public const decimal MinimumAmount = 1_000m;
    
    /// <summary>
    /// Maximum allowed payment amount in VND
    /// </summary>
    public const decimal MaximumAmount = 100_000_000m;
    
    /// <summary>
    /// Amount must be divisible by this value (1,000 VND)
    /// </summary>
    public const decimal AmountRoundingUnit = 1_000m;
    
    /// <summary>
    /// Maximum description length for PayOS (truncated if longer)
    /// </summary>
    public const int MaxDescriptionLength = 25;
    
    /// <summary>
    /// Default transaction expiration time in minutes
    /// </summary>
    public const int DefaultExpirationMinutes = 30;
}
