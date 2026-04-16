using AgoraIO.Media;
using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Infrastructure.Configurations;

namespace SFARS.Infrastructure.Services;

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

        uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        uint privilegeExpiredTs = now + (uint)expirationSeconds;

        return RtcTokenBuilder.buildTokenWithUID(
            _options.AppId,
            _options.AppCertificate,
            channelName,
            uid,
            RtcTokenBuilder.Role.RolePublisher,
            privilegeExpiredTs);
    }
}
