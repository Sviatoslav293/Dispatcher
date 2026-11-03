using DispatcherServer.Persistence;
using DispatcherServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DispatcherServer.Tests;

public sealed class DispatcherSchemaTests
{
    private static DispatcherDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DispatcherSchemaModelTest;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        return new DispatcherDbContext(options);
    }

    [Fact]
    public void Model_MapsExactlyTheExistingTablesAndColumns()
    {
        using var context = CreateContext();
        var expected = new Dictionary<string, string[]>
        {
            ["Aircraft"] = ["id", "registrationNumber", "model", "aircraftType"],
            ["AircraftPositions"] = ["id", "aircraftId", "latitude", "longitude", "altitude", "speed", "heading", "updatedAt"],
            ["AirspaceSectors"] = ["id", "name", "minAltitude", "maxAltitude", "boundaryData"],
            ["ATCCommands"] = ["id", "commandId", "flightId", "commandType", "parameters", "issuedAt", "status"],
            ["Flights"] = ["id", "aircraftId", "routeId", "callsign", "status", "scheduledDeparture", "actualDeparture", "scheduledArrival", "actualArrival"],
            ["Routes"] = ["id", "name", "description"],
            ["RouteWaypoints"] = ["routeId", "waypointId", "sequenceNumber"],
            ["SystemEvents"] = ["id", "flightId", "eventType", "description", "occurredAt"],
            ["Waypoints"] = ["id", "name", "latitude", "longitude"]
        };

        var entityTypes = context.Model.GetEntityTypes().ToArray();
        Assert.Equal(expected.Count, entityTypes.Length);
        foreach (var (table, columns) in expected)
        {
            var entity = Assert.Single(entityTypes, entityType =>
                entityType.GetTableName() == table && entityType.GetSchema() == "dbo");
            var storeObject = StoreObjectIdentifier.Table(table, "dbo");
            Assert.Equal(columns.Order(), entity.GetProperties().Select(property => property.GetColumnName(storeObject)).Order());
        }

        Assert.Equal(["routeId", "sequenceNumber"], context.Model.FindEntityType(typeof(RouteWaypoint))!
            .FindPrimaryKey()!.Properties.Select(property => property.GetColumnName(StoreObjectIdentifier.Table("RouteWaypoints", "dbo"))));
        Assert.Equal("datetime2(0)", context.Model.FindEntityType(typeof(Flight))!
            .FindProperty(nameof(Flight.ScheduledDeparture))!.GetColumnType());
        Assert.Equal("datetime2(3)", context.Model.FindEntityType(typeof(AircraftPosition))!
            .FindProperty(nameof(AircraftPosition.UpdatedAt))!.GetColumnType());
        Assert.True(context.Model.FindEntityType(typeof(AtcCommand))!
            .FindProperty(nameof(AtcCommand.Parameters))!.IsNullable);
        var commandEntity = context.Model.FindEntityType(typeof(AtcCommand))!;
        Assert.True(commandEntity.FindProperty(nameof(AtcCommand.CommandId))!.IsNullable);
        var commandIdIndex = Assert.Single(commandEntity.GetIndexes(), index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(AtcCommand.CommandId));
        Assert.True(commandIdIndex.IsUnique);
        Assert.Equal("[commandId] IS NOT NULL", commandIdIndex.GetFilter());
        Assert.True(context.Model.FindEntityType(typeof(Flight))!
            .FindProperty(nameof(Flight.ActualDeparture))!.IsNullable);
    }
}
