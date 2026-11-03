using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Plugins;

public sealed class AircraftCommandModule(
    IServiceScopeFactory scopeFactory,
    IAtcCommandRepository commandRepository,
    IAircraftCommandPublisher commandPublisher,
    ILogger<AircraftCommandModule> logger) : IServerModule
{
    private static readonly Type[] EventTypes =
    [
        typeof(AircraftCommandRequested),
        typeof(AircraftCommandResultReceived)
    ];

    public int ExecutionOrder => 300;
    public IReadOnlyCollection<Type> HandledEventTypes => EventTypes;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task HandleEventAsync(IServerEvent serverEvent, CancellationToken cancellationToken)
    {
        switch (serverEvent)
        {
            case AircraftCommandRequested requested:
                await HandleRequestAsync(requested, cancellationToken);
                break;
            case AircraftCommandResultReceived received:
                var resultOutcome = await HandleResultAsync(received.Result, cancellationToken);
                received.Completion.TrySetResult(resultOutcome);
                break;
            default:
                throw new ArgumentException($"Unsupported event type '{serverEvent.GetType().Name}'.", nameof(serverEvent));
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleRequestAsync(AircraftCommandRequested requested, CancellationToken cancellationToken)
    {
        if (!Enum.GetNames<AircraftCommandType>().Contains(requested.Type, StringComparer.OrdinalIgnoreCase)
            || !Enum.TryParse<AircraftCommandType>(requested.Type, ignoreCase: true, out var type)
            || !IsValueValid(type, requested.Value))
        {
            requested.Completion.TrySetResult(new AircraftCommandRequestOutcome(
                AircraftCommandRequestStatus.Invalid,
                Message: "Невідомий тип команди або недопустиме значення."));
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var flightRepository = scope.ServiceProvider.GetRequiredService<IFlightRepository>();
        var flight = await flightRepository.GetByIdAsync(requested.FlightId, cancellationToken);
        if (flight is null)
        {
            requested.Completion.TrySetResult(new AircraftCommandRequestOutcome(
                AircraftCommandRequestStatus.FlightNotFound,
                Message: "Рейс не знайдено."));
            return;
        }

        if (!string.Equals(flight.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            requested.Completion.TrySetResult(new AircraftCommandRequestOutcome(
                AircraftCommandRequestStatus.FlightNotControllable,
                Message: "Керувати можна лише активним рейсом."));
            return;
        }

        var issuedAtUtc = DateTime.UtcNow;
        var command = new AircraftCommand(
            Guid.NewGuid(),
            flight.AircraftId,
            flight.Id,
            type,
            requested.Value,
            issuedAtUtc,
            issuedAtUtc.AddMinutes(2));

        await commandRepository.CreatePendingAsync(command, cancellationToken);
        try
        {
            await commandPublisher.PublishAsync(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await commandRepository.MarkPublishFailedAsync(command.CommandId, exception.Message, cancellationToken);
            logger.LogError(exception, "Failed to publish aircraft command {CommandId}.", command.CommandId);
            requested.Completion.TrySetResult(new AircraftCommandRequestOutcome(
                AircraftCommandRequestStatus.PublishFailed,
                command.CommandId,
                "Не вдалося передати команду літаку."));
            return;
        }

        await commandRepository.MarkSentAsync(command.CommandId, cancellationToken);
        requested.Completion.TrySetResult(new AircraftCommandRequestOutcome(
            AircraftCommandRequestStatus.Accepted,
            command.CommandId));
    }

    private async Task<CommandResultPersistenceOutcome> HandleResultAsync(
        AircraftCommandResult result,
        CancellationToken cancellationToken)
    {
        var outcome = await commandRepository.ApplyResultAsync(result, cancellationToken);
        switch (outcome)
        {
            case CommandResultPersistenceOutcome.Applied:
            case CommandResultPersistenceOutcome.AlreadyFinal:
                return outcome;
            case CommandResultPersistenceOutcome.NotFound:
                logger.LogWarning("Ignoring result for unknown command {CommandId}.", result.CommandId);
                return outcome;
            case CommandResultPersistenceOutcome.AircraftMismatch:
            case CommandResultPersistenceOutcome.FlightMismatch:
                logger.LogWarning(
                    "Ignoring result for command {CommandId} because its aircraft or flight does not match the stored command.",
                    result.CommandId);
                return outcome;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown command result persistence outcome.");
        }
    }

    private static bool IsValueValid(AircraftCommandType type, double value)
    {
        if (!double.IsFinite(value))
        {
            return false;
        }

        return type switch
        {
            AircraftCommandType.SetHeading => value is >= 0 and < 360,
            AircraftCommandType.SetAltitude => value is >= 0 and <= 60_000,
            AircraftCommandType.SetSpeed => value is >= 0 and <= 1_000,
            _ => false
        };
    }
}
