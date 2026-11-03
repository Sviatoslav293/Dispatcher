using System.Text.Json;
using AircraftSimulator.Core;
using AircraftSimulator.Plugins;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class RabbitMqTelemetryFormatterTests
{
    [Fact]
    public void Serialize_UsesWireNamesStringStatusAndUtcTimestamp()
    {
        var telemetry = new AircraftTelemetry(
            1001,
            251001,
            50.45,
            30.52,
            7200,
            650,
            130,
            FlightStatus.EnRoute,
            new DateTimeOffset(2025, 1, 2, 10, 0, 0, TimeSpan.FromHours(2)));

        using var document = JsonDocument.Parse(RabbitMqTelemetryFormatter.Serialize(telemetry));
        var root = document.RootElement;

        Assert.Equal(1001, root.GetProperty("aircraftId").GetInt32());
        Assert.Equal(251001, root.GetProperty("flightId").GetInt32());
        Assert.Equal(50.45, root.GetProperty("latitude").GetDouble());
        Assert.Equal(30.52, root.GetProperty("longitude").GetDouble());
        Assert.Equal(7200, root.GetProperty("altitudeMeters").GetDouble());
        Assert.Equal(650, root.GetProperty("speedKmh").GetDouble());
        Assert.Equal(130, root.GetProperty("headingDegrees").GetDouble());
        Assert.Equal("EnRoute", root.GetProperty("status").GetString());
        Assert.Equal(
            new DateTimeOffset(2025, 1, 2, 8, 0, 0, TimeSpan.Zero),
            root.GetProperty("timestampUtc").GetDateTimeOffset());
        Assert.Equal(9, root.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(1001, "aircraft.1001.telemetry")]
    [InlineData(1099, "aircraft.1099.telemetry")]
    public void CreateRoutingKey_UsesAircraftId(int aircraftId, string expected)
    {
        Assert.Equal(expected, RabbitMqTelemetryFormatter.CreateRoutingKey(aircraftId));
    }
}
