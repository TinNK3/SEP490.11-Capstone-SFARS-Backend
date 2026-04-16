using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Helpers.Agora;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Senior-level implementation of Agora RTC Token generation (Access Token v2 logic).
/// Decoupled from heavy SDKs, using verified packing logic.
/// </summary>
public class AgoraService : IAgoraService
{
    private readonly AgoraOptions _options;

    public AgoraService(IOptions<AgoraOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateRtcToken(string channelName, uint uid, int expirationSeconds = 3600)
    {
        if (string.IsNullOrEmpty(_options.AppId) || string.IsNullOrEmpty(_options.AppCertificate))
        {
            return string.Empty;
        }

        // Initialize AccessToken2 with provided credentials and expiration
        var token = new AccessToken2(_options.AppId, _options.AppCertificate, (uint)expirationSeconds);

        // Create RTC service for the specific channel and user
        // Using string representation for Uid as per AccessToken2.ServiceRtc requirements
        var serviceRtc = new AccessToken2.ServiceRtc(channelName, uid.ToString());

        // Add standard video call privileges
        uint privilegeExpirationTs = (uint)expirationSeconds;
        serviceRtc.AddPrivilegeRtc(AccessToken2.PrivilegeRtcEnum.JoinChannel, privilegeExpirationTs);
        serviceRtc.AddPrivilegeRtc(AccessToken2.PrivilegeRtcEnum.PublishAudioStream, privilegeExpirationTs);
        serviceRtc.AddPrivilegeRtc(AccessToken2.PrivilegeRtcEnum.PublishVideoStream, privilegeExpirationTs);
        serviceRtc.AddPrivilegeRtc(AccessToken2.PrivilegeRtcEnum.PublishDataStream, privilegeExpirationTs);

        // Append service to token
        token.AddService(serviceRtc);

        // Build final token string
        return token.Build();
    }
}