using System.Text.Json;
using System.Text.Json.Serialization;
using DispatcherServer.Core;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DispatcherServer.Messaging;

public sealed class RabbitMqAircraftCommandPublisher(
    RabbitMqConsumerOptions options,
    ILogger<RabbitMqAircraftCommandPublisher> logger) : IAircraftCommandPublisher
{
    public const string ExchangeName = "aircraft.commands";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string GetRoutingKey(int aircraftId) => $"aircraft.{aircraftId}.command";

    public async Task PublishAsync(AircraftCommand command, CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        try
        {
            await using var connection = await connectionFactory.CreateConnectionAsync(
                "DispatcherServer aircraft command publisher",
                cancellationToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
            await channel.ExchangeDeclareAsync(
                ExchangeName,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var body = Serialize(command);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = command.CommandId.ToString()
            };

            await channel.BasicPublishAsync(
                ExchangeName,
                GetRoutingKey(command.AircraftId),
                mandatory: true,
                basicProperties: properties,
                body,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "RabbitMQ did not confirm command {CommandId} for aircraft {AircraftId}.",
                command.CommandId, command.AircraftId);
            throw;
        }
    }

    public static byte[] Serialize(AircraftCommand command) => JsonSerializer.SerializeToUtf8Bytes(command with
    {
        IssuedAtUtc = command.IssuedAtUtc.ToUniversalTime(),
        ExpiresAtUtc = command.ExpiresAtUtc.ToUniversalTime()
    }, JsonOptions);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return jsonOptions;
    }
}
