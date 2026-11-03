using AircraftSimulator.Core;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AircraftSimulator.Plugins;

public sealed class RabbitMqTelemetryPublisher : ITelemetryPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTelemetryPublisher> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqTelemetryPublisher(
        RabbitMqOptions options,
        ILogger<RabbitMqTelemetryPublisher> logger)
    {
        options.Validate();
        _options = options;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
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
    }

    public async Task PublishAsync(AircraftTelemetry telemetry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureChannelAsync(cancellationToken);

            var routingKey = RabbitMqTelemetryFormatter.CreateRoutingKey(telemetry.AircraftId);
            var body = RabbitMqTelemetryFormatter.Serialize(telemetry);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString("N")
            };

            await _channel!.BasicPublishAsync(
                _options.ExchangeName,
                routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
            _logger.LogInformation(
                "Published telemetry for aircraft {AircraftId}, flight {FlightId} to {Exchange} with routing key {RoutingKey}.",
                telemetry.AircraftId,
                telemetry.FlightId,
                _options.ExchangeName,
                routingKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish telemetry for aircraft {AircraftId}, flight {FlightId}.",
                telemetry.AircraftId,
                telemetry.FlightId);
            throw;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publishLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not { IsOpen: true })
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = await _connectionFactory.CreateConnectionAsync(
                "AircraftSimulator telemetry publisher",
                cancellationToken);
            _logger.LogInformation(
                "Connected to RabbitMQ at {HostName}:{Port}, virtual host {VirtualHost}.",
                _options.HostName,
                _options.Port,
                _options.VirtualHost);
        }

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
        _channel.BasicReturnAsync += HandleBasicReturnAsync;
        await _channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
    }

    private Task HandleBasicReturnAsync(object sender, BasicReturnEventArgs eventArgs)
    {
        _logger.LogError(
            "RabbitMQ returned an unroutable telemetry message from exchange {Exchange} with routing key {RoutingKey}: {ReplyCode} {ReplyText}.",
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.ReplyCode,
            eventArgs.ReplyText);
        return Task.CompletedTask;
    }
}
