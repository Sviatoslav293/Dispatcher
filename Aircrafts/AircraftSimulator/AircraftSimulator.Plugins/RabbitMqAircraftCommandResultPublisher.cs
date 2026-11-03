using AircraftSimulator.Core;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AircraftSimulator.Plugins;

public sealed class RabbitMqAircraftCommandResultPublisher : IAircraftCommandResultPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqAircraftCommandResultPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqAircraftCommandResultPublisher(RabbitMqOptions options, ILogger<RabbitMqAircraftCommandResultPublisher> logger)
    {
        _options = options;
        _logger = logger;
        _factory = new ConnectionFactory
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

    public async Task PublishAsync(AircraftCommandResult result, CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                if (_channel is not null) await _channel.DisposeAsync();
                if (_connection is not { IsOpen: true })
                {
                    if (_connection is not null) await _connection.DisposeAsync();
                    _connection = await _factory.CreateConnectionAsync("AircraftSimulator command result publisher", cancellationToken);
                }

                _channel = await _connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    cancellationToken);
                await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
                    cancellationToken: cancellationToken);
            }

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = result.CommandId.ToString("N")
            };
            await _channel!.BasicPublishAsync(_options.ExchangeName,
                RabbitMqAircraftCommandFormatter.CreateCommandResultRoutingKey(result.AircraftId),
                mandatory: true, basicProperties: properties, body: RabbitMqAircraftCommandFormatter.SerializeResult(result),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Published command result {CommandId} with status {Status}.", result.CommandId, result.Status);
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
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }
}
