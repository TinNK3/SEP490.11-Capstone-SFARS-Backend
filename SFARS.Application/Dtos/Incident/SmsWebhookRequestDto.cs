namespace SFARS.Application.Dtos.Incident;

public class SmsWebhookRequestDto
{
    /// <summary>
    /// The phone number of the victim who sent the SOS SMS.
    /// E.g., +84987654321
    /// </summary>
    public string SenderPhone { get; set; } = null!;

    /// <summary>
    /// The exact text content of the SMS message.
    /// Expecting: "SFARS SOS [lat],[lng]"
    /// </summary>
    public string MessageBody { get; set; } = null!;

    /// <summary>
    /// Secret key set on the DIY SMS Gateway app settings,
    /// Must match the backend's AppSettings for authentication.
    /// </summary>
    public string SecretKey { get; set; } = null!;
}