using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using DispatcherServer.Persistence;
using DispatcherServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DispatcherServer.Tests;

public sealed class TelemetryPersistenceTests
{
    [Fact]
    public async Task PositionPersistence_InsertsThenUpdatesAndIgnoresStaleWithoutDuplicates()
    {
        await using var fixture = new PersistenceTestFixture();
        await fixture.SeedAsync(1099);
        var persistence = new AircraftPositionPersistence(fixture.ContextFactory,
            NullLogger<AircraftPositionPersistence>.Instance);

        await persistence.SaveLatestPositionsAsync([Snapshot(1099, 1, 50.1, "EnRoute")]);
        await persistence.SaveLatestPositionsAsync([Snapshot(1099, 3, 50.3, "EnRoute")]);
        await persistence.SaveLatestPositionsAsync([Snapshot(1099, 2, 50.2, "EnRoute")]);

        await using var context = fixture.CreateContext();
        var positions = await context.AircraftPositions.AsNoTracking().ToListAsync();
        var position = Assert.Single(positions);
        Assert.Equal(50.3m, position.Latitude);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 3, DateTimeKind.Utc), position.UpdatedAt);
    }

    [Fact]
    public async Task PositionPersistence_SkipsAircraftMissingFromDatabase()
    {
        await using var fixture = new PersistenceTestFixture();
        var persistence = new AircraftPositionPersistence(fixture.ContextFactory,
            NullLogger<AircraftPositionPersistence>.Instance);

        await persistence.SaveLatestPositionsAsync([Snapshot(9999, 1, 50.1, "EnRoute")]);

        await using var context = fixture.CreateContext();
        Assert.Empty(await context.AircraftPositions.ToListAsync());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(359.99, 359.99)]
    [InlineData(359.999999, 0)]
    [InlineData(360, 0)]
    [InlineData(-0.001, 0)]
    [InlineData(720, 0)]
    public async Task PositionPersistence_NormalizesHeadingToDatabaseRange(double inputHeading, decimal expectedHeading)
    {
        await using var fixture = new PersistenceTestFixture();
        await fixture.SeedAsync(1099);
        var persistence = new AircraftPositionPersistence(fixture.ContextFactory,
            NullLogger<AircraftPositionPersistence>.Instance);

        var position = Snapshot(1099, 1, 50.1, "EnRoute") with { HeadingDegrees = inputHeading };
        await persistence.SaveLatestPositionsAsync([position]);

        await using var context = fixture.CreateContext();
        var savedPosition = await context.AircraftPositions.SingleAsync(item => item.AircraftId == 1099);
        Assert.Equal(expectedHeading, savedPosition.Heading);
        Assert.InRange(savedPosition.Heading, 0m, 359.99m);
    }

    [Theory]
    [InlineData("Scheduled", "EnRoute", "Active", "FlightStarted")]
    [InlineData("Active", "RouteCompleted", "Completed", "FlightCompleted")]
    [InlineData("Active", "Landed", "Completed", "FlightCompleted")]
    public async Task FlightStatusPersistence_RecordsOnlyValidStatusTransitions(
        string existingStatus, string telemetryStatus, string expectedStatus, string expectedEvent)
    {
        await using var fixture = new PersistenceTestFixture();
        await fixture.SeedAsync(1099);
        await SeedFlightAsync(fixture, 251099, 1099, existingStatus);
        var persistence = new FlightStatusPersistence(fixture.ContextFactory,
            NullLogger<FlightStatusPersistence>.Instance);

        var telemetry = new AircraftTelemetryReceived(1099, 251099, 50, 30, 3000, 350, 0,
            telemetryStatus, new DateTimeOffset(2026, 1, 1, 0, 0, 10, TimeSpan.Zero));
        await persistence.RecordStatusAsync(telemetry);
        await persistence.RecordStatusAsync(telemetry);

        await using var context = fixture.CreateContext();
        var flight = await context.Flights.SingleAsync(item => item.Id == 251099);
        var systemEvent = Assert.Single(await context.SystemEvents.ToListAsync());
        Assert.Equal(expectedStatus, flight.Status);
        Assert.Equal(expectedEvent, systemEvent.EventType);
    }

    [Fact]
    public async Task FlightStatusPersistence_UnknownFlightDoesNotCreateEvent()
    {
        await using var fixture = new PersistenceTestFixture();
        var persistence = new FlightStatusPersistence(fixture.ContextFactory,
            NullLogger<FlightStatusPersistence>.Instance);

        await persistence.RecordStatusAsync(new AircraftTelemetryReceived(1099, 251099, 50, 30, 3000, 350, 0,
            "RouteCompleted", DateTimeOffset.UtcNow));

        await using var context = fixture.CreateContext();
        Assert.Empty(await context.SystemEvents.ToListAsync());
    }

    [Fact]
    public void PersistenceFailureException_RetainsUnderlyingFailure()
    {
        var databaseFailure = new InvalidOperationException("Database unavailable.");
        var exception = new PersistenceUnavailableException("Retry persistence.", databaseFailure);

        Assert.Same(databaseFailure, exception.InnerException);
    }

    [Fact]
    public async Task StateStore_RestoresHistoricalSnapshotAndOnlyReplacesWithNewerTelemetry()
    {
        var stateStore = new DispatcherServer.Plugins.InMemoryAircraftStateStore();
        Assert.True(stateStore.Restore(Snapshot(1099, 10, 50.1, "Historical")));
        Assert.True(stateStore.TryGetByAircraftId(1099, out var historical));
        Assert.True(historical!.IsHistorical);

        Assert.False(stateStore.Update(new AircraftTelemetryReceived(1099, 251099, 50, 30, 3000, 350, 0,
            "EnRoute", historical.TimestampUtc.AddSeconds(-1))));
        Assert.True(stateStore.Update(new AircraftTelemetryReceived(1099, 251099, 50.2, 30, 3000, 350, 0,
            "EnRoute", historical.TimestampUtc.AddSeconds(1))));
        Assert.True(stateStore.TryGetByAircraftId(1099, out var active));
        Assert.False(active!.IsHistorical);
        Assert.Equal(50.2, active.Latitude);
    }

    [Fact]
    public async Task PositionHostedService_RestoresOnStartAndSavesOnGracefulStop()
    {
        await using var fixture = new PersistenceTestFixture();
        await fixture.SeedAsync(1099);
        var persistence = new AircraftPositionPersistence(fixture.ContextFactory,
            NullLogger<AircraftPositionPersistence>.Instance);
        var initial = Snapshot(1099, 10, 50.1, "EnRoute");
        await persistence.SaveLatestPositionsAsync([initial]);
        var store = new DispatcherServer.Plugins.InMemoryAircraftStateStore();
        var service = new DispatcherServer.Api.AircraftPositionPersistenceHostedService(
            store, persistence, NullLogger<DispatcherServer.Api.AircraftPositionPersistenceHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        Assert.True(store.TryGetByAircraftId(1099, out var restored));
        Assert.True(restored!.IsHistorical);

        store.Update(new AircraftTelemetryReceived(1099, 251099, 50.2, 30.5, 3000, 350, 180,
            "EnRoute", initial.TimestampUtc.AddSeconds(1)));
        await service.StopAsync(CancellationToken.None);

        await using var context = fixture.CreateContext();
        var saved = await context.AircraftPositions.SingleAsync(position => position.AircraftId == 1099);
        Assert.Equal(50.2m, saved.Latitude);
        Assert.Equal(initial.TimestampUtc.AddSeconds(1).UtcDateTime, saved.UpdatedAt);
    }

    [Fact]
    public async Task TelemetryPersistenceModule_WrapsDatabaseFailureForRetry()
    {
        var telemetry = new AircraftTelemetryReceived(1099, 251099, 50, 30, 3000, 350, 0,
            "EnRoute", DateTimeOffset.UtcNow);
        var store = new DispatcherServer.Plugins.InMemoryAircraftStateStore();
        store.Update(telemetry);
        var module = new TelemetryPersistenceModule(new FailingFlightStatusPersistence(), store,
            NullLogger<TelemetryPersistenceModule>.Instance);

        var exception = await Assert.ThrowsAsync<PersistenceUnavailableException>(
            () => module.HandleEventAsync(telemetry, CancellationToken.None));

        Assert.IsType<DbUpdateException>(exception.InnerException);
    }

    private static AircraftTelemetrySnapshot Snapshot(int aircraftId, int second, double latitude, string status) =>
        new(aircraftId, 251099, latitude, 30.5, 3000, 350, 180, status,
            new DateTimeOffset(2026, 1, 1, 0, 0, second, TimeSpan.Zero));

    private static async Task SeedFlightAsync(PersistenceTestFixture fixture, int flightId, int aircraftId, string status)
    {
        await using var context = fixture.CreateContext();
        context.Flights.Add(new Flight
        {
            Id = flightId,
            AircraftId = aircraftId,
            RouteId = 1099,
            Callsign = "TEST1099",
            Status = status,
            ScheduledDeparture = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledArrival = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
    }

    private sealed class FailingFlightStatusPersistence : IFlightStatusPersistence
    {
        public Task<bool> RecordStatusAsync(AircraftTelemetryReceived telemetry, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Database unavailable.");
    }
}
