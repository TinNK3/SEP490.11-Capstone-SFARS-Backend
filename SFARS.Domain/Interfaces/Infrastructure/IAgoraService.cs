namespace SFARS.Domain.Interfaces.Infrastructure;

public interface IAgoraService
{
    /// <summary>
    /// Generates an Agora RTC token for a specific channel and user.
    /// </summary>
    /// <param name="channelName">The channel name (usually IncidentId or Code).</param>
    /// <param name="uid">The unique user ID (numeric for Agora SDK).</param>
    /// <param name="expirationSeconds">Token validity period.</param>
    /// <returns>The generated token string.</returns>
    string GenerateRtcToken(string channelName, uint uid, int expirationSeconds = 3600);
}