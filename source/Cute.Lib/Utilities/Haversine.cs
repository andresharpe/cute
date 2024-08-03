namespace Cute.Lib.Utilities;

public class Haversine
{
    private const double _earthRadius = 6371.0f;  // earth "radius" in km

    private const double _degreesToRadiansFactor = Math.PI / 180.0f;

    private const double _radiansToDegreesFactor = 180.0f / Math.PI;

    public static BoundingBox GetBoundingBox(double lon, double lat, double radiusInKm)
    {
        var radiusRatio = radiusInKm / _earthRadius;
        var latOffset = radiusRatio * _radiansToDegreesFactor;
        var lonOffset = Math.Asin(radiusRatio) / Math.Cos(lat * _degreesToRadiansFactor) * _radiansToDegreesFactor;

        return new BoundingBox
        (
            MaxLat: lat + latOffset,
            MinLat: lat - latOffset,
            MaxLon: lon + lonOffset,
            MinLon: lon - lonOffset
        );
    }

    public static double DistanceInKm(double lon1, double lon2, double lat1, double lat2)
    {
        var φ1 = lat1 * _degreesToRadiansFactor;

        var φ2 = lat2 * _degreesToRadiansFactor;

        var Δφ = (lat2 - lat1) * _degreesToRadiansFactor;

        var Δλ = (lon2 - lon1) * _degreesToRadiansFactor;

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        var d = _earthRadius * c;

        return d;
    }
}

public record BoundingBox(double MaxLon, double MinLon, double MaxLat, double MinLat)
{
    public bool Contains(double lon, double lat)
    {
        return lon > MinLon && lon < MaxLon && lat > MinLat && lat < MaxLat;
    }
};