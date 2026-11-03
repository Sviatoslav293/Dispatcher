namespace DispatcherServer.Messaging;

public sealed class RabbitMqConsumerOptions
{
    public string HostName { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "aircraft.events";
    public string QueueName { get; set; } = "dispatcher.telemetry";
    public string RoutingKey { get; set; } = "aircraft.*.telemetry";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
        {
            throw new InvalidOperationException("RabbitMq:HostName must be configured.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("RabbitMq:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new InvalidOperationException("RabbitMq:UserName must be configured.");
        }

        if (string.IsNullOrEmpty(Password))
        {
            throw new InvalidOperationException("RabbitMq:Password must be supplied through configuration or an environment variable.");
        }

        if (string.IsNullOrWhiteSpace(VirtualHost)
            || string.IsNullOrWhiteSpace(ExchangeName)
            || string.IsNullOrWhiteSpace(QueueName)
            || string.IsNullOrWhiteSpace(RoutingKey))
        {
            throw new InvalidOperationException("RabbitMq:VirtualHost, ExchangeName, QueueName, and RoutingKey must be configured.");
        }
    }
}
