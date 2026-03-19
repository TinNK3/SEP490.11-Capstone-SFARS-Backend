namespace SFARS.Domain.Interfaces.Services
{
    /// <summary>
    /// Provides in-memory revocation of JWT access tokens by their JTI claim.
    /// Tokens are stored until their natural expiry so the blacklist never grows unboundedly.
    /// Can be replaced later with a distributed cache (Redis) without changing callers.
    /// </summary>
    public interface ITokenBlacklistService
    {
        /// <summary>
        /// Revoke a token. Entries are automatically removed when <paramref name="expiry"/> is reached.
        /// </summary>
        void Revoke(string jti, DateTime expiry);

        /// <summary>
        /// Returns <c>true</c> if the JTI has been revoked and the token must be rejected.
        /// </summary>
        bool IsRevoked(string jti);
    }
}
