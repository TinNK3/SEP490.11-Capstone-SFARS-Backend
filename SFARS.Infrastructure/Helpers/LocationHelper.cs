using System.Security.Cryptography;
using NetTopologySuite.Geometries;
using SFARS.Domain.Common.Constants;

namespace SFARS.Infrastructure.Helpers;

/// <summary>
/// Static helper for location-related computations.
/// All distance calculations use Haversine (NOT Point.Distance which returns degrees).
/// </summary>
public static class LocationHelper
{
    private const double EarthRadiusMeters = 6_371_000;

    #region Haversine Distance

    /// <summary>
    /// Haversine distance in METERS between two NTS Points.
    /// Point.X = Longitude, Point.Y = Latitude.
    /// </summary>
    public static double HaversineMeters(Point p1, Point p2)
        => HaversineMeters(p1.Y, p1.X, p2.Y, p2.X);

    /// <summary>
    /// Haversine distance in METERS between two lat/lng pairs.
    /// </summary>
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    #endregion

    #region Accuracy Level

    /// <summary>
    /// Maps raw accuracy meters to a UI-friendly level string.
    /// </summary>
    public static string GetAccuracyLevel(double? accuracyMeters)
    {
        if (!accuracyMeters.HasValue) return LocationConstants.AccuracyUnknown;
        return accuracyMeters.Value switch
        {
            < LocationConstants.AccuracyHighThresholdMeters   => LocationConstants.AccuracyHigh,
            < LocationConstants.AccuracyMediumThresholdMeters => LocationConstants.AccuracyMedium,
            _                                                  => LocationConstants.AccuracyLow
        };
    }

    #endregion

    #region Tracking Code

    /// <summary>
    /// Generate a cryptographically secure tracking code.
    /// 18 bytes → ~24 base64url chars → 2^144 combinations.
    /// </summary>
    public static string GenerateTrackingCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    #endregion

    #region Tracking Log Sampling

    /// <summary>
    /// Determines if a tracking log should be written based on movement and time.
    /// Rules: skip if accuracy > 200m, log if moved ≥ 250m OR ≥ 180s elapsed.
    /// Thresholds are tuned for optimal DB IOPS (~80 records per 20km trip).
    /// </summary>
    public static bool ShouldLogTracking(
        Point? newLocation, Point? lastLocation, DateTime? lastLoggedAt,
        double? accuracyMeters)
    {
        if (newLocation == null) return false;
        if (lastLocation == null || !lastLoggedAt.HasValue) return true; // First log

        // Skip if accuracy too poor
        if (accuracyMeters.HasValue && accuracyMeters.Value > LocationConstants.AccuracyDiscardThresholdMeters)
            return false;

        var distanceMeters = HaversineMeters(newLocation, lastLocation);
        var secondsSince = (DateTime.UtcNow - lastLoggedAt.Value).TotalSeconds;

        // Log if moved enough OR enough time elapsed
        return distanceMeters >= LocationConstants.TrackingLogMinMovementMeters
            || secondsSince >= LocationConstants.TrackingLogMinIntervalSeconds;
    }

    #endregion

    #region Bounding Box

    /// <summary>
    /// Compute a lat/lng bounding box for a given center + radius.
    /// Used as a SQL pre-filter to reduce rows before Haversine.
    /// Returns (minLat, maxLat, minLng, maxLng).
    /// </summary>
    public static (double MinLat, double MaxLat, double MinLng, double MaxLng)
        BoundingBox(double lat, double lng, double radiusKm)
    {
        // 1 degree of latitude ≈ 111.32 km
        var latDelta = radiusKm / 111.32;
        // 1 degree of longitude varies with latitude
        var lngDelta = radiusKm / (111.32 * Math.Cos(ToRad(lat)));

        return (
            MinLat: lat - latDelta,
            MaxLat: lat + latDelta,
            MinLng: lng - lngDelta,
            MaxLng: lng + lngDelta);
    }

    #endregion
}