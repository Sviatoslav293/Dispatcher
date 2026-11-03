using AircraftSimulator.Core;
using AircraftSimulator.Host;
using AircraftSimulator.Plugins;

var builder = Host.CreateApplicationBuilder(args);

var aircraftConfigurationPath = builder.Configuration["AircraftConfiguration:Path"] ?? "aircraft.json";
var resolvedConfigurationPath = Path.GetFullPath(aircraftConfigurationPath, builder.Environment.ContentRootPath);

builder.Services.AddSingleton<IAircraftConfigurationSource>(new LocalConfigurationSource(resolvedConfigurationPath));
builder.Services.AddSingleton<ISimulationClock, SystemSimulationClock>();
builder.Services.AddSingleton<IAircraftCommandQueue, AircraftCommandQueue>();
builder.Services.AddSingleton<CommandModuleOptions>(builder.Configuration.GetSection("Commands").Get<CommandModuleOptions>()
    ?? CommandModuleOptions.Default);
var movementOptions = builder.Configuration.GetSection("Movement").Get<MovementOptions>() ?? new MovementOptions();
var navigationOptions = builder.Configuration.GetSection("Navigation").Get<NavigationOptions>() ?? new NavigationOptions();
builder.Services.AddSingleton(movementOptions);
builder.Services.AddSingleton(navigationOptions);
var telemetryPublisher = builder.Configuration["Telemetry:Publisher"] ?? "Logger";
RabbitMqOptions? rabbitMqOptions = null;
switch (telemetryPublisher.Trim().ToLowerInvariant())
{
    case "logger":
        builder.Services.AddSingleton<ITelemetryPublisher, LoggerTelemetryPublisher>();
        break;
    case "rabbitmq":
        rabbitMqOptions = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        rabbitMqOptions.Validate();
        builder.Services.AddSingleton(rabbitMqOptions);
        builder.Services.AddSingleton<ITelemetryPublisher, RabbitMqTelemetryPublisher>();
        break;
    default:
        throw new InvalidOperationException($"Unsupported telemetry publisher '{telemetryPublisher}'. Use 'Logger' or 'RabbitMq'.");
}

var commandReceivingEnabled = builder.Configuration.GetValue<bool>("Commands:Enabled");
if (commandReceivingEnabled)
{
    if (rabbitMqOptions is null)
    {
        throw new InvalidOperationException("Command receiving requires Telemetry:Publisher to be RabbitMq.");
    }

    builder.Services.AddSingleton<IAircraftCommandResultPublisher, RabbitMqAircraftCommandResultPublisher>();
    builder.Services.AddSingleton<IAircraftModule, CommandModule>();
    builder.Services.AddSingleton<IAircraftModule>(services => new NavigationModule(
        services.GetRequiredService<NavigationOptions>(),
        services.GetRequiredService<MovementOptions>()));
    builder.Services.AddSingleton<IAircraftModule>(services => new MovementModule(
        services.GetRequiredService<MovementOptions>()));
    builder.Services.AddSingleton<IAircraftModule, TelemetryModule>();
    builder.Services.AddHostedService(services => new RabbitMqAircraftCommandReceiver(
        services.GetRequiredService<RabbitMqOptions>(),
        services.GetRequiredService<IAircraftConfigurationSource>(),
        services.GetRequiredService<IAircraftCommandQueue>(),
        services.GetRequiredService<ILogger<RabbitMqAircraftCommandReceiver>>()));
}

if (!commandReceivingEnabled)
{
    builder.Services.AddSingleton<IAircraftModule>(services => new NavigationModule(
        services.GetRequiredService<NavigationOptions>(),
        services.GetRequiredService<MovementOptions>()));
    builder.Services.AddSingleton<IAircraftModule>(services => new MovementModule(services.GetRequiredService<MovementOptions>()));
    builder.Services.AddSingleton<IAircraftModule, TelemetryModule>();
}
builder.Services.AddSingleton<IAircraftSimulator, AircraftSimulator.Core.AircraftSimulator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
