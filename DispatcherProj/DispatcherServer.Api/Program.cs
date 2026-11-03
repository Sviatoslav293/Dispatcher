using DispatcherServer.Api;
using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using DispatcherServer.Messaging;
using DispatcherServer.Persistence;
using DispatcherServer.Plugins;

var builder = WebApplication.CreateBuilder(args);
var isTestHost = builder.Environment.IsEnvironment("Testing");
var allowMissingDatabase = isTestHost;

var rabbitMqOptions = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqConsumerOptions>()
    ?? new RabbitMqConsumerOptions();
rabbitMqOptions.Validate();

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton<TelemetryMessageDeserializer>();
builder.Services.AddSingleton(new PersistenceRetryOptions());
builder.Services.AddSingleton<IAircraftStateStore, InMemoryAircraftStateStore>();
builder.Services.AddSingleton<IServerModule, AircraftMonitoringModule>();
var dispatcherDbConnectionString = builder.Configuration.GetConnectionString("DispatcherDb");
if (!string.IsNullOrWhiteSpace(dispatcherDbConnectionString))
{
    builder.Services.AddDispatcherPersistence(dispatcherDbConnectionString);
    builder.Services.AddSingleton<IServerModule, TelemetryPersistenceModule>();
    builder.Services.AddSingleton<IAircraftCommandPublisher, RabbitMqAircraftCommandPublisher>();
    builder.Services.AddSingleton<IServerModule, AircraftCommandModule>();
    builder.Services.AddHostedService<AircraftPositionPersistenceHostedService>();
}
else if (!allowMissingDatabase)
{
    throw new InvalidOperationException("ConnectionStrings:DispatcherDb must be configured for telemetry persistence.");
}
builder.Services.AddSingleton<ServerMicrokernel>();
builder.Services.AddSingleton<IServerEventDispatcher>(services => services.GetRequiredService<ServerMicrokernel>());
if (!isTestHost)
{
    builder.Services.AddHostedService<ServerMicrokernelHostedService>();
    builder.Services.AddHostedService<TelemetryConsumerHostedService>();
    if (!string.IsNullOrWhiteSpace(dispatcherDbConnectionString))
    {
        builder.Services.AddHostedService<AircraftCommandResultConsumerHostedService>();
    }
}
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/api/aircraft", (IAircraftStateStore stateStore) => Results.Ok(stateStore.GetAll()));
app.MapGet("/api/aircraft/{id:int}", (int id, IAircraftStateStore stateStore) =>
    stateStore.TryGetByAircraftId(id, out var telemetry)
        ? Results.Ok(telemetry)
        : Results.NotFound());
app.MapGet("/api/flights", async (IFlightRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAllAsync(cancellationToken)));
app.MapGet("/api/flights/{id:int}", async (int id, IFlightRepository repository, CancellationToken cancellationToken) =>
{
    var flight = await repository.GetByIdAsync(id, cancellationToken);
    return flight is null ? Results.NotFound() : Results.Ok(flight);
});
app.MapPost("/api/flights/{flightId:int}/commands", async (
    int flightId,
    CreateAircraftCommandRequest request,
    IServerEventDispatcher eventDispatcher,
    CancellationToken cancellationToken) =>
{
    var completion = new TaskCompletionSource<AircraftCommandRequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
    try
    {
        await eventDispatcher.DispatchAsync(
            new AircraftCommandRequested(flightId, request.Type, request.Value, completion),
            cancellationToken);
        var outcome = await completion.Task.WaitAsync(cancellationToken);
        return outcome.Status switch
        {
            AircraftCommandRequestStatus.Accepted => Results.Accepted(
                $"/api/commands/{outcome.CommandId}",
                new { commandId = outcome.CommandId }),
            AircraftCommandRequestStatus.Invalid => Results.BadRequest(new { message = outcome.Message }),
            AircraftCommandRequestStatus.FlightNotFound => Results.NotFound(new { message = outcome.Message }),
            AircraftCommandRequestStatus.FlightNotControllable => Results.Conflict(new { message = outcome.Message }),
            AircraftCommandRequestStatus.PublishFailed => Results.Json(
                new { commandId = outcome.CommandId, message = outcome.Message },
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Problem(
                title: "Не вдалося обробити команду.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    catch (ServerModuleException)
    {
        return Results.Json(
            new { message = "Сервіс команд тимчасово недоступний." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/api/flights/{flightId:int}/commands", async (
    int flightId,
    IAtcCommandRepository repository,
    CancellationToken cancellationToken) =>
{
    var commands = await repository.GetByFlightIdAsync(flightId, cancellationToken);
    return Results.Ok(commands.Select(ToResponse));
});
app.MapGet("/api/commands/{commandId:guid}", async (
    Guid commandId,
    IAtcCommandRepository repository,
    CancellationToken cancellationToken) =>
{
    var command = await repository.GetByCommandIdAsync(commandId, cancellationToken);
    return command is null ? Results.NotFound() : Results.Ok(ToResponse(command));
});
app.MapGet("/api/routes/{id:int}", async (int id, IRouteRepository repository, CancellationToken cancellationToken) =>
{
    var route = await repository.GetByIdAsync(id, cancellationToken);
    return route is null ? Results.NotFound() : Results.Ok(route);
});
app.MapHealthChecks("/health");

app.Run();

static AircraftCommandResponse ToResponse(AtcCommandRecord command) => new(
    command.CommandId,
    command.FlightId,
    command.AircraftId,
    command.Type.ToString(),
    command.Value,
    command.IssuedAtUtc,
    command.ExpiresAtUtc,
    command.Status.ToString(),
    command.ResultMessage);

public partial class Program
{
}

public sealed record CreateAircraftCommandRequest(string Type, double Value);

public sealed record AircraftCommandResponse(
    Guid CommandId,
    int FlightId,
    int AircraftId,
    string Type,
    double Value,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    string Status,
    string? ResultMessage);
