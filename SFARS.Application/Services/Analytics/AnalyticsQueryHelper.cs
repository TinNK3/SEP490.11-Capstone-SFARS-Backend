using SFARS.Domain.Entities.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services.Analytics;

internal static class AnalyticsQueryHelper
{
    internal static TimeZoneInfo ResolveIctTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }

    internal static (DateTime? StartUtcInclusive, DateTime? EndUtcExclusive) NormalizeUtcRange(AnalyticsSpecParams filter)
    {
        if (!filter.StartDate.HasValue && !filter.EndDate.HasValue) return (null, null);

        var tz = ResolveIctTimeZone();

        DateTime? startUtc = null;
        if (filter.StartDate.HasValue)
        {
            var localStart = filter.StartDate.Value;
            if (localStart.Kind == DateTimeKind.Unspecified) localStart = DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified);
            startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        }

        DateTime? endUtcExclusive = null;
        if (filter.EndDate.HasValue)
        {
            var localEndExclusive = filter.EndDate.Value.Date.AddDays(1);
            localEndExclusive = DateTime.SpecifyKind(localEndExclusive, DateTimeKind.Unspecified);
            endUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, tz);
        }

        return (startUtc, endUtcExclusive);
    }

    internal static IQueryable<T> ApplyCreatedAtUtcRange<T>(IQueryable<T> query, AnalyticsSpecParams filter)
        where T : BaseEntity
    {
        var (startUtc, endUtcExclusive) = NormalizeUtcRange(filter);
        if (startUtc.HasValue) query = query.Where(x => x.CreatedAt >= startUtc.Value);
        if (endUtcExclusive.HasValue) query = query.Where(x => x.CreatedAt < endUtcExclusive.Value);
        return query;
    }
}

