using System.Text.Json;
using AircraftSimulator.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AircraftSimulator.Plugins;

public sealed class RabbitMqAircraftCommandReceiver : BackgroundService
{
    public const string CommandsExchangeName = "aircraft.commands";

    private readonly RabbitMqOptions _options;
    private readonly IAircraftConfigurationSource _configurationSource;
    private readonly IAircraftCommandQueue _commandQueue;
    private readonly ILogger<RabbitMqAircraftCommandReceiver> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqAircraftCommandReceiver(
        RabbitMqOptions options,
        IAircraftConfigurationSource configurationSource,
        IAircraftCommandQueue commandQueue,
        ILogger<RabbitMqAircraftCommandReceiver> logger)
    {
        _options = options;
        _options.Validate();
        _configurationSource = configurationSource;
        _commandQueue = commandQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var aircraftConfiguration = await _configurationSource.LoadAsync(stoppingToken);
        var aircraftId = aircraftConfiguration.InitialState.AircraftId;
        var flightId = aircraftConfiguration.InitialState.FlightId;
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _connection = await factory.CreateConnectionAsync("AircraftSimulator command receiver", stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(CommandsExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);

        var queueName = FormattableString.Invariant($"aircraft.commands.{aircraftId}");
        var routingKey = RabbitMqAircraftCommandFormatter.CreateCommandRoutingKey(aircraftId);
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, CommandsExchangeName, routingKey, arguments: null,
            cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(delivery, aircraftId, flightId, stoppingToken);
        await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer, stoppingToken);
        _logger.LogInformation("Receiving dispatcher commands for AircraftId={AircraftId}, FlightId={FlightId} from {QueueName}.",
            aircraftId, flightId, queueName);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private async Task HandleDeliveryAsync(BasicDeliverEventArgs delivery, int aircraftId, int flightId, CancellationToken cancellationToken)
    {
        try
        {
            var command = RabbitMqAircraftCommandFormatter.DeserializeCommand(delivery.Body.Span);
            var validation = Validate(command, aircraftId, flightId);
            if (!_commandQueue.TryEnqueue(new AircraftCommandWorkItem(command, validation)))
            {
                await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                return;
            }

            await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Rejecting malformed dispatcher command for AircraftId={AircraftId}.", aircraftId);
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Command delivery {DeliveryTag} failed; rejecting without requeue.", delivery.DeliveryTag);
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }

    private static string? Validate(AircraftCommand command, int aircraftId, int flightId)
    {
        if (command.CommandId == Guid.Empty) return "CommandId must not be empty.";
        if (command.AircraftId != aircraftId) return "AircraftId does not match this simulator.";
        if (command.FlightId != flightId) return "FlightId does not match this simulator.";
        if (!Enum.IsDefined(command.Type)) return "Command type is not supported.";
        if (!double.IsFinite(command.Value)) return "Command value must be finite.";
        if (command.IssuedAtUtc.Kind != DateTimeKind.Utc || command.ExpiresAtUtc.Kind != DateTimeKind.Utc)
            return "IssuedAtUtc and ExpiresAtUtc must be UTC timestamps.";
        var now = DateTime.UtcNow;
        if (command.ExpiresAtUtc <= now) return "Command has expired.";
        if (command.ExpiresAtUtc <= command.IssuedAtUtc) return "ExpiresAtUtc must be later than IssuedAtUtc.";
        return null;
    }

}
