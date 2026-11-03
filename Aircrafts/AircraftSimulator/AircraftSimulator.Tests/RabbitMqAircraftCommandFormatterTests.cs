using System.Text.Json;
using AircraftSimulator.Core;
using AircraftSimulator.Plugins;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class RabbitMqAircraftCommandFormatterTests
{
    [Fact]
    public void DeserializeCommand_UsesCamelCasePropertiesAndStringEnum()
    {
        var commandId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            commandId,
            aircraftId = 1099,
            flightId = 251099,
            type = "setHeading",
            value = 359.999,
            issuedAtUtc = DateTime.UtcNow,
            expiresAtUtc = DateTime.UtcNow.AddMinutes(1)
        });

        var command = RabbitMqAircraftCommandFormatter.DeserializeCommand(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(AircraftCommandType.SetHeading, command.Type);
        Assert.Equal("aircraft.1099.command", RabbitMqAircraftCommandFormatter.CreateCommandRoutingKey(1099));
        Assert.Equal("aircraft.1099.command-result", RabbitMqAircraftCommandFormatter.CreateCommandResultRoutingKey(1099));
    }

    [Fact]
    public void SerializeResult_UsesCamelCaseAndStringEnum()
    {
        var result = new AircraftCommandResult(Guid.NewGuid(), 1099, 251099,
            AircraftCommandResultStatus.Applied, "Applied", DateTime.UtcNow);

        using var document = JsonDocument.Parse(RabbitMqAircraftCommandFormatter.SerializeResult(result));

        Assert.Equal("applied", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.TryGetProperty("timestampUtc", out _));
        Assert.True(document.RootElement.TryGetProperty("commandId", out _));
    }
}
