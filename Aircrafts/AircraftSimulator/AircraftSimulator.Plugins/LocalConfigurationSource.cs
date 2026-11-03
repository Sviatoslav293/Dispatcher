using System.Text.Json;
using System.Text.Json.Serialization;
using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public sealed class LocalConfigurationSource(string filePath) : IAircraftConfigurationSource
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AircraftConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Aircraft configuration file was not found: '{filePath}'.", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var configuration = await JsonSerializer.DeserializeAsync<AircraftConfigurationFile>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException($"Aircraft configuration file '{filePath}' is empty or invalid.");

        Validate(configuration);
        var state = new AircraftState(
            configuration.AircraftId,
            configuration.FlightId,
            configuration.Latitude,
            configuration.Longitude,
            configuration.AltitudeMeters,
            configuration.SpeedKmh,
            configuration.HeadingDegrees,
            configuration.Status);
        return new AircraftConfiguration(state, configuration.Route);
    }

    private static void Validate(AircraftConfigurationFile configuration)
    {
        if (configuration.AircraftId <= 0)
        {
            throw new InvalidDataException("AircraftId must be greater than zero.");
        }

        if (configuration.FlightId <= 0)
        {
            throw new InvalidDataException("FlightId must be greater than zero.");
        }

        ValidateCoordinates(configuration.Latitude, configuration.Longitude, "initial aircraft position");
        if (!double.IsFinite(configuration.AltitudeMeters) || configuration.AltitudeMeters < 0)
        {
            throw new InvalidDataException("AltitudeMeters must be a finite, non-negative value.");
        }

        if (!double.IsFinite(configuration.SpeedKmh) || configuration.SpeedKmh < 0)
        {
            throw new InvalidDataException("SpeedKmh must be a finite, non-negative value.");
        }

        if (!double.IsFinite(configuration.HeadingDegrees) || configuration.HeadingDegrees is < 0 or >= 360)
        {
            throw new InvalidDataException("HeadingDegrees must be in the range [0, 360).");
        }

        if (!Enum.IsDefined(configuration.Status))
        {
            throw new InvalidDataException($"Status '{configuration.Status}' is not a valid flight status.");
        }

        if (configuration.Route is null || configuration.Route.Count == 0)
        {
            throw new InvalidDataException("Route must contain at least one waypoint.");
        }

        if (configuration.Route.Any(waypoint => waypoint is null))
        {
            throw new InvalidDataException("Route must not contain null waypoint entries.");
        }

        if (configuration.Route.Any(waypoint => waypoint.Id <= 0))
        {
            throw new InvalidDataException("Every waypoint ID must be greater than zero.");
        }

        if (configuration.Route.Select(waypoint => waypoint.Id).Distinct().Count() != configuration.Route.Count)
        {
            throw new InvalidDataException("Waypoint IDs must be unique.");
        }

        foreach (var waypoint in configuration.Route)
        {
            ValidateCoordinates(waypoint.Latitude, waypoint.Longitude, $"waypoint {waypoint.Id}");
            if (waypoint.TargetAltitudeMeters is < 0 || waypoint.TargetAltitudeMeters is { } altitude && !double.IsFinite(altitude))
            {
                throw new InvalidDataException($"TargetAltitudeMeters for waypoint {waypoint.Id} must be finite and non-negative.");
            }
        }
    }

    private static void ValidateCoordinates(double latitude, double longitude, string description)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new InvalidDataException($"Latitude for {description} must be in the range [-90, 90].");
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new InvalidDataException($"Longitude for {description} must be in the range [-180, 180].");
        }
    }

    private sealed class AircraftConfigurationFile
    {
        public int AircraftId { get; init; }
        public int FlightId { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double AltitudeMeters { get; init; }
        public double SpeedKmh { get; init; }
        public double HeadingDegrees { get; init; }
        public FlightStatus Status { get; init; }
        public List<Waypoint> Route { get; init; } = [];
    }
}
