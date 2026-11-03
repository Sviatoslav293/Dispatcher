using System.Text.Json;
using System.Text.Json.Serialization;
using DispatcherServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DispatcherServer.Messaging;

public sealed class AircraftCommandResultConsumerHostedService : BackgroundService
{
    public const string ExchangeName = "aircraft.events";
    public const string QueueName = "dispatcher.command-results";
    public const string RoutingKey = "aircraft.*.command-result";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly RabbitMqConsumerOptions _options;
    private readonly IServerEventDispatcher _eventDispatcher;
    private readonly PersistenceRetryOptions _retryOptions;
    private readonly ILogger<AircraftCommandResultConsumerHostedService> _logger;

    public AircraftCommandResultConsumerHostedService(
        RabbitMqConsumerOptions options,
        IServerEventDispatcher eventDispatcher,
        PersistenceRetryOptions retryOptions,
        ILogger<AircraftCommandResultConsumerHostedService> logger)
    {
        options.Validate();
        _options = options;
        _eventDispatcher = eventDispatcher;
        _retryOptions = retryOptions;
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
            "DispatcherServer command result consumer",
            stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            RoutingKey,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => ProcessDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        _logger.LogInformation(
            "Consuming command results from queue {QueueName}, bound to {ExchangeName} with {RoutingKey}.",
            QueueName, ExchangeName, RoutingKey);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        AircraftCommandResult result;
        try
        {
            result = JsonSerializer.Deserialize<AircraftCommandResult>(delivery.Body.Span, JsonOptions)
                ?? throw new JsonException("Command result payload was empty.");
            if (!Enum.IsDefined(result.Status))
            {
                throw new JsonException("Command result status is not supported.");
            }

            result = result with { TimestampUtc = result.TimestampUtc.ToUniversalTime() };
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            _logger.LogError(exception, "Rejecting malformed command result delivery {DeliveryTag}.", delivery.DeliveryTag);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        try
        {
            var completion = new TaskCompletionSource<CommandResultPersistenceOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _eventDispatcher.DispatchAsync(new AircraftCommandResultReceived(result, completion), cancellationToken);
            var outcome = await completion.Task.WaitAsync(cancellationToken);
            if (outcome is CommandResultPersistenceOutcome.Applied or CommandResultPersistenceOutcome.AlreadyFinal)
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            else
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Command result delivery {DeliveryTag} was not saved; it will be retried after {Delay}.",
                delivery.DeliveryTag, _retryOptions.Delay);
            await Task.Delay(_retryOptions.Delay, cancellationToken);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return jsonOptions;
    }
}
