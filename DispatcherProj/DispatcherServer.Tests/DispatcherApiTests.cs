using System.Net;
using System.Net.Http.Json;
using DispatcherServer.Core.Persistence;

namespace DispatcherServer.Tests;

public sealed class DispatcherApiTests : IClassFixture<DispatcherApiFactory>
{
    private readonly HttpClient _client;

    public DispatcherApiTests(DispatcherApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetFlights_ReturnsFlightDtos()
    {
        var flights = await _client.GetFromJsonAsync<FlightRecord[]>("/api/flights");

        Assert.NotNull(flights);
        Assert.Single(flights);
        Assert.Equal("TEST123", flights[0].Callsign);
    }

    [Fact]
    public async Task GetFlight_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/flights/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoute_ReturnsWaypointsInSequenceOrder()
    {
        var route = await _client.GetFromJsonAsync<RouteRecord>("/api/routes/2");

        Assert.NotNull(route);
        Assert.Equal([1, 2], route.Waypoints.Select(waypoint => waypoint.SequenceNumber));
    }

    [Fact]
    public async Task GetRoute_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/routes/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
