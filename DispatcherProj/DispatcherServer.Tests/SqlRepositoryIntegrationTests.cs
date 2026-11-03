using DispatcherServer.Persistence;
using DispatcherServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DispatcherServer.Tests;

public sealed class SqlRepositoryIntegrationTests
{
    private static DispatcherDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("DISPATCHER_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("DISPATCHER_TEST_CONNECTION_STRING is required.");
        var options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure(0))
            .Options;
        return new DispatcherDbContext(options);
    }

    [DatabaseFact]
    public async Task DatabaseSchema_MatchesMappedTablesAndColumns()
    {
        await using var context = CreateContext();
        var actualColumns = await context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(TABLE_NAME, N'.', COLUMN_NAME) AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = N'dbo'")
            .ToListAsync();
        var expectedColumns = context.Model.GetEntityTypes()
            .SelectMany(entity =>
            {
                var storeObject = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(
                    entity.GetTableName()!, entity.GetSchema());
                return entity.GetProperties().Select(property => $"{entity.GetTableName()}.{property.GetColumnName(storeObject)}");
            })
            .Order()
            .ToArray();

        Assert.Equal(expectedColumns, actualColumns.Order());
    }

    [DatabaseFact]
    public async Task AircraftRepository_ReadsExistingAircraftWithoutWriting()
    {
        await using var context = CreateContext();
        var aircraft = await new AircraftRepository(context).GetAllAsync();

        Assert.NotNull(aircraft);
    }

    [DatabaseFact]
    public async Task FlightRepository_ReadsExistingFlightsWithoutWriting()
    {
        await using var context = CreateContext();
        var flights = await new FlightRepository(context).GetAllAsync();

        Assert.NotNull(flights);
    }

    [DatabaseFact]
    public async Task RouteRepository_LoadsWaypointsInSequenceOrder()
    {
        await using var context = CreateContext();
        var routeId = await context.Routes.AsNoTracking()
            .Where(route => route.RouteWaypoints.Any())
            .Select(route => (int?)route.Id)
            .FirstOrDefaultAsync();

        if (routeId is null)
        {
            return;
        }

        var route = await new RouteRepository(context).GetByIdAsync(routeId.Value);

        Assert.NotNull(route);
        var sequences = route.Waypoints.Select(waypoint => waypoint.SequenceNumber).ToArray();
        Assert.Equal(sequences.Order(), sequences);
    }

    [Fact]
    public async Task FlightRepository_UnreachableServer_ThrowsSqlException()
    {
        var options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DispatcherDB;User Id=invalid;Password=invalid;Connect Timeout=1;Encrypt=False")
            .Options;
        await using var context = new DispatcherDbContext(options);
        var repository = new FlightRepository(context);

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => repository.GetAllAsync());
    }
}
