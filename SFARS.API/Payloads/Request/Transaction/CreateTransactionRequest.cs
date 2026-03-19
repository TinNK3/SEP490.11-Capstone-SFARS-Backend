namespace SFARS.API.Payloads.Request.Transaction;

/// <summary>
/// Request payload to create a new donation (pending transaction).
/// </summary>
public class CreateTransactionRequest
{
    /// <summary>
    /// Donation amount in VND. Must be between 10,000 and 100,000,000.
    /// Will be rounded to the nearest 1,000 VND.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional message / description from the donor (max 25 chars displayed on PayOS).
    /// </summary>
    public string? Description { get; set; }
}
