using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;
using System.Collections.Concurrent;

namespace SFARS.Application.Services;

public class SystemMessageService : ISystemMessageService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemMessageService> _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SystemMessageService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<SystemMessageService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetMessageAsync(string msgId)
    {
        if (string.IsNullOrWhiteSpace(msgId))
        {
            return string.Empty;
        }

        try
        {
            // Retrieve global language setting
            var langStr = LanguageContext.CurrentLanguage ?? SystemLanguage.Vietnamese.GetDescription();
            var langEnum = EnumExtensions.GetValueFromDescription<SystemLanguage>(langStr);
            
            var cacheKey = $"SystemMessage_{msgId}_{langEnum}";

            // 1. Fast path: check cache first without locking
            if (_cache.TryGetValue(cacheKey, out string? cachedMessage))
            {
                return cachedMessage!;
            }

            // 2. Cache stampede prevention using SemaphoreSlim per key
            var myLock = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await myLock.WaitAsync();

            try
            {
                // 3. Double-checked locking
                if (_cache.TryGetValue(cacheKey, out cachedMessage))
                {
                    return cachedMessage!;
                }

                // 4. Create an isolated scope independent of the current HTTP Context's DbContext.
                // This guarantees no "A second operation was started on this context" exception
                // when called from ExceptionHandlingMiddleware after an existing DB transaction fails.
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var spec = new BaseSpecification<SystemMessage>(x => x.MsgId == msgId);
                var msgEntity = await unitOfWork.Repository<SystemMessage, Guid>()
                    .GetWithSpecAsync(spec, tracked: false);

                // 5. Select language version or fallback to MsgId
                string message = langEnum switch
                {
                    SystemLanguage.Vietnamese => msgEntity?.Vi ?? $"[{msgId}]",
                    SystemLanguage.English => msgEntity?.En ?? $"[{msgId}]",
                    _ => msgEntity?.Vi ?? $"[{msgId}]"
                };

                // 6. Persist to cache (Sliding 12h, Absolute 24h)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24))
                    .SetSlidingExpiration(TimeSpan.FromHours(12));

                _cache.Set(cacheKey, message, cacheOptions);

                return message;
            }
            finally
            {
                myLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system message for MsgId: {MsgId}", msgId);
            return $"[{msgId}]"; // Graceful fallback
        }
    }
}