using Microsoft.Extensions.Logging;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Interfaces.Infrastructure;
using StackExchange.Redis;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Redis-backed SOS anti-spam guard.
/// Uses two keys per user:
///   "sfars:sos:spam:{userId}"  — INCR counter with rolling TTL (cancellation count)
///   "sfars:sos:block:{userId}" — Presence key that hard-blocks SOS creation
///
/// Uses StackExchange.Redis directly, consistent with RedisLocationCacheService.
/// </summary>
public class SosSpamGuardService : ISosSpamGuardService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SosSpamGuardService> _logger;

    public SosSpamGuardService(
        IConnectionMultiplexer redis,
        ILogger<SosSpamGuardService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SosSpamStatus> CheckAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Check hard block first (cheapest path)
            var blockKey = SosConstants.RedisBlockKeyPrefix + userId;
            if (await db.KeyExistsAsync(blockKey))
                return SosSpamStatus.HardBlocked;

            // Read cancellation counter
            var spamKey = SosConstants.RedisSpamKeyPrefix + userId;
            var raw = await db.StringGetAsync(spamKey);
            var count = raw.HasValue ? (int)raw : 0;

            return count >= SosConstants.SoftCancelThreshold
                ? SosSpamStatus.SoftWarning
                : SosSpamStatus.Allowed;
        }
        catch (Exception ex)
        {
            // Fail-open: Redis down → allow SOS (safety critical feature)
            _logger.LogWarning(ex, "SosSpamGuard.CheckAsync failed for userId={UserId} — failing open", userId);
            return SosSpamStatus.Allowed;
        }
    }

    /// <inheritdoc />
    public async Task RecordCancellationAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var spamKey = SosConstants.RedisSpamKeyPrefix + userId;

            // Atomic INCR — returns new value
            var newCount = await db.StringIncrementAsync(spamKey);

            // Set/refresh the TTL on every increment (sliding window)
            await db.KeyExpireAsync(spamKey, SosConstants.SpamWindowTtl);

            _logger.LogInformation(
                "SOS cancellation recorded. UserId={UserId}, Count={Count}", userId, newCount);

            // Promote to hard block if threshold crossed
            if (newCount >= SosConstants.HardCancelThreshold)
            {
                var blockKey = SosConstants.RedisBlockKeyPrefix + userId;

                // SET NX EX — only set if not already blocked
                var wasSet = await db.StringSetAsync(
                    blockKey, "1",
                    SosConstants.BlockDuration,
                    When.NotExists);

                if (wasSet)
                {
                    _logger.LogWarning(
                        "User hard-blocked from SOS for {Minutes} minutes. UserId={UserId}",
                        SosConstants.BlockDuration.TotalMinutes, userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SosSpamGuard.RecordCancellationAsync failed for userId={UserId}", userId);
        }
    }

    /// <inheritdoc />
    public async Task ClearBlockAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(SosConstants.RedisBlockKeyPrefix + userId);
            await db.KeyDeleteAsync(SosConstants.RedisSpamKeyPrefix + userId);

            _logger.LogInformation("SOS block cleared for userId={UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SosSpamGuard.ClearBlockAsync failed for userId={UserId}", userId);
        }
    }
}