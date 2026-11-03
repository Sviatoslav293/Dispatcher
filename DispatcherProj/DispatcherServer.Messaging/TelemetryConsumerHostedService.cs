using DispatcherServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DispatcherServer.Messaging;

public sealed class TelemetryConsumerHostedService : BackgroundService
{
    private readonly RabbitMqConsumerOptions _options;
    private readonly IServerEventDispatcher _eventDispatcher;
    private readonly TelemetryMessageDeserializer _deserializer;
    private readonly ILogger<TelemetryConsumerHostedService> _logger;
    private readonly PersistenceRetryOptions _persistenceRetryOptions;

    public TelemetryConsumerHostedService(
        RabbitMqConsumerOptions options,
        IServerEventDispatcher eventDispatcher,
        TelemetryMessageDeserializer deserializer,
        PersistenceRetryOptions persistenceRetryOptions,
        ILogger<TelemetryConsumerHostedService> logger)
    {
        options.Validate();
        _options = options;
        _eventDispatcher = eventDispatcher;
        _deserializer = deserializer;
        _persistenceRetryOptions = persistenceRetryOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionFactory = new ConnectionFactory
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

        await using var connection = await connectionFactory.CreateConnectionAsync(
            "DispatcherServer telemetry consumer",
            stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            _options.QueueName,
            _options.ExchangeName,
            _options.RoutingKey,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => ProcessDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(
            _options.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);

        _logger.LogInformation(
            "Consuming telemetry from RabbitMQ queue {QueueName}, bound to {ExchangeName} with {RoutingKey}.",
            _options.QueueName,
            _options.ExchangeName,
            _options.RoutingKey);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            var telemetryEvent = _deserializer.Deserialize(delivery.Body);
            await _eventDispatcher.DispatchAsync(telemetryEvent, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ServerModuleException exception) when (exception.InnerException is PersistenceUnavailableException)
        {
            await RetryPersistenceDeliveryAsync(channel, delivery, exception, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Telemetry delivery {DeliveryTag} was not processed successfully and will be rejected without requeue.",
                delivery.DeliveryTag);
            try
            {
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception rejectException)
            {
                _logger.LogCritical(
                    rejectException,
                    "Could not reject telemetry delivery {DeliveryTag}; the RabbitMQ channel may be unavailable.",
                    delivery.DeliveryTag);
                throw;
            }
        }
    }

    private async Task RetryPersistenceDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception,
            "SQL persistence failed for delivery {DeliveryTag}; requeueing after {Delay}. Configure a RabbitMQ dead-letter/retry policy for bounded retries.",
            delivery.DeliveryTag, _persistenceRetryOptions.Delay);
        try
        {
            await Task.Delay(_persistenceRetryOptions.Delay, cancellationToken);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception requeueException)
        {
            _logger.LogCritical(requeueException,
                "Could not requeue delivery {DeliveryTag} after SQL failure; RabbitMQ will redeliver it if the channel closes before acknowledgement.",
                delivery.DeliveryTag);
            throw;
        }
    }
}
