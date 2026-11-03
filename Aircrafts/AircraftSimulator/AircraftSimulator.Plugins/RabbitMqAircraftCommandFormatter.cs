using System.Text.Json;
using System.Text.Json.Serialization;
using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public static class RabbitMqAircraftCommandFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string CreateCommandRoutingKey(int aircraftId)
    {
        if (aircraftId <= 0) throw new ArgumentOutOfRangeException(nameof(aircraftId));
        return FormattableString.Invariant($"aircraft.{aircraftId}.command");
    }

    public static string CreateCommandResultRoutingKey(int aircraftId)
    {
        if (aircraftId <= 0) throw new ArgumentOutOfRangeException(nameof(aircraftId));
        return FormattableString.Invariant($"aircraft.{aircraftId}.command-result");
    }

    public static AircraftCommand DeserializeCommand(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<AircraftCommand>(body, SerializerOptions)
        ?? throw new JsonException("Command body was empty.");

    public static byte[] SerializeResult(AircraftCommandResult result) =>
        JsonSerializer.SerializeToUtf8Bytes(result, SerializerOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
