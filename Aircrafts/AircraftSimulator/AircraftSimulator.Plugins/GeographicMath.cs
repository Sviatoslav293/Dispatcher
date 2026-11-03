namespace AircraftSimulator.Plugins;

public static class GeographicMath
{
    public const double EarthRadiusMeters = 6_371_000;

    public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitudeDelta = ToRadians(latitude2 - latitude1);
        var longitudeDelta = ToRadians(longitude2 - longitude1);
        var firstLatitude = ToRadians(latitude1);
        var secondLatitude = ToRadians(latitude2);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(Math.Max(0, 1 - haversine)));
        return EarthRadiusMeters * centralAngle;
    }

    public static double DistanceToSegmentMeters(
        double pointLatitude,
        double pointLongitude,
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude)
    {
        var referenceLatitude = ToRadians((pointLatitude + startLatitude + endLatitude) / 3);
        var startToEndX = ToRadians(NormalizeLongitude(endLongitude - startLongitude)) * Math.Cos(referenceLatitude) * EarthRadiusMeters;
        var startToEndY = ToRadians(endLatitude - startLatitude) * EarthRadiusMeters;
        var startToPointX = ToRadians(NormalizeLongitude(pointLongitude - startLongitude)) * Math.Cos(referenceLatitude) * EarthRadiusMeters;
        var startToPointY = ToRadians(pointLatitude - startLatitude) * EarthRadiusMeters;
        var segmentLengthSquared = startToEndX * startToEndX + startToEndY * startToEndY;
        if (segmentLengthSquared == 0)
        {
            return Math.Sqrt(startToPointX * startToPointX + startToPointY * startToPointY);
        }

        var projection = Math.Clamp(
            (startToPointX * startToEndX + startToPointY * startToEndY) / segmentLengthSquared,
            0,
            1);
        var differenceX = startToPointX - projection * startToEndX;
        var differenceY = startToPointY - projection * startToEndY;
        return Math.Sqrt(differenceX * differenceX + differenceY * differenceY);
    }

    public static double BearingDegrees(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var firstLatitude = ToRadians(latitude1);
        var secondLatitude = ToRadians(latitude2);
        var longitudeDelta = ToRadians(longitude2 - longitude1);
        var y = Math.Sin(longitudeDelta) * Math.Cos(secondLatitude);
        var x = Math.Cos(firstLatitude) * Math.Sin(secondLatitude)
            - Math.Sin(firstLatitude) * Math.Cos(secondLatitude) * Math.Cos(longitudeDelta);
        return NormalizeDegrees(ToDegrees(Math.Atan2(y, x)));
    }

    public static (double Latitude, double Longitude) DestinationPoint(double latitude, double longitude, double bearingDegrees, double distanceMeters)
    {
        var angularDistance = distanceMeters / EarthRadiusMeters;
        var bearing = ToRadians(bearingDegrees);
        var startLatitude = ToRadians(latitude);
        var startLongitude = ToRadians(longitude);
        var destinationLatitude = Math.Asin(
            Math.Sin(startLatitude) * Math.Cos(angularDistance)
            + Math.Cos(startLatitude) * Math.Sin(angularDistance) * Math.Cos(bearing));
        var destinationLongitude = startLongitude + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(startLatitude),
            Math.Cos(angularDistance) - Math.Sin(startLatitude) * Math.Sin(destinationLatitude));

        return (ToDegrees(destinationLatitude), NormalizeLongitude(ToDegrees(destinationLongitude)));
    }

    public static double TurnTowards(double currentHeadingDegrees, double targetHeadingDegrees, double maximumTurnDegrees)
    {
        if (maximumTurnDegrees < 0 || !double.IsFinite(maximumTurnDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTurnDegrees));
        }

        var difference = SignedAngleDifferenceDegrees(currentHeadingDegrees, targetHeadingDegrees);
        return NormalizeDegrees(currentHeadingDegrees + Math.Clamp(difference, -maximumTurnDegrees, maximumTurnDegrees));
    }

    public static double NormalizeDegrees(double degrees) => (degrees % 360 + 360) % 360;

    public static double AbsoluteAngleDifferenceDegrees(double firstDegrees, double secondDegrees)
        => Math.Abs(SignedAngleDifferenceDegrees(firstDegrees, secondDegrees));

    private static double SignedAngleDifferenceDegrees(double fromDegrees, double toDegrees)
    {
        var difference = NormalizeDegrees(toDegrees - fromDegrees);
        return difference > 180 ? difference - 360 : difference;
    }

    private static double NormalizeLongitude(double longitude) => (longitude + 540) % 360 - 180;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
