using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Application.Services.Auth
{
    /// <summary>
    /// In-memory JWT token blacklist backed by IMemoryCache.
    /// Each revoked JTI is kept until the token's natural expiry so the list never grows unboundedly.
    /// Register as Singleton so the blacklist survives across DI scopes.
    /// </summary>
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private const string KeyPrefix = "revoked_jti:";

        private readonly IMemoryCache _cache;
        private readonly ILogger<TokenBlacklistService> _logger;

        public TokenBlacklistService(IMemoryCache cache, ILogger<TokenBlacklistService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <inheritdoc />
        public void Revoke(string jti, DateTime expiry)
        {
            var ttl = expiry - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero)
            {
                // Token already expired — blacklisting it would be pointless
                _logger.LogDebug("[TokenBlacklist] Skipped revoke for already-expired JTI: {Jti}", jti);
                return;
            }

            _cache.Set(KeyPrefix + jti, true, ttl);
            _logger.LogDebug("[TokenBlacklist] JTI revoked: {Jti} — will auto-expire in {Ttl}",
                jti, ttl.ToString(@"mm\:ss"));
        }

        /// <inheritdoc />
        public bool IsRevoked(string jti)
        {
            var revoked = _cache.TryGetValue(KeyPrefix + jti, out _);
            if (revoked)
            {
                _logger.LogDebug("[TokenBlacklist] JTI {Jti} → REVOKED (user signed out)", jti);
            }
            else
            {
                _logger.LogDebug("[TokenBlacklist] JTI {Jti} → valid (not blacklisted)", jti);
            }
            return revoked;
        }
    }
}
