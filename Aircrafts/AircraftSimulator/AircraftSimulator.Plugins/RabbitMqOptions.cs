namespace AircraftSimulator.Plugins;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "aircraft.events";

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

        if (string.IsNullOrWhiteSpace(VirtualHost))
        {
            throw new InvalidOperationException("RabbitMq:VirtualHost must be configured.");
        }

        if (string.IsNullOrWhiteSpace(ExchangeName))
        {
            throw new InvalidOperationException("RabbitMq:ExchangeName must be configured.");
        }
    }
}
