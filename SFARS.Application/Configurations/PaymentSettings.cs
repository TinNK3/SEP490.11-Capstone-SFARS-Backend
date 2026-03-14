namespace SFARS.Application.Configurations;

/// <summary>
/// General payment system settings independent of specific gateway.
/// </summary>
public class PaymentSettings
{
    /// <summary>
    /// Number of minutes before a pending transaction expires.
    /// </summary>
    public int TransactionExpiredInMinutes { get; set; } = 30;
}
