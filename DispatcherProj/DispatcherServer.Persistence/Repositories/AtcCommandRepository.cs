using System.Text.Json;
using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using DispatcherServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DispatcherServer.Persistence.Repositories;

public sealed class AtcCommandRepository(IDbContextFactory<DispatcherDbContext> contextFactory) : IAtcCommandRepository
{
    private static readonly JsonSerializerOptions ParameterJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CreatePendingAsync(AircraftCommand command, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.AtcCommands.Add(new AtcCommand
        {
            CommandId = command.CommandId,
            FlightId = command.FlightId,
            CommandType = command.Type.ToString(),
            Parameters = SerializeParameters(command.AircraftId, command.Value, command.ExpiresAtUtc),
            IssuedAt = command.IssuedAtUtc,
            Status = AtcCommandStatus.Pending.ToString()
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSentAsync(Guid commandId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.AtcCommands
            .Where(command => command.CommandId == commandId && command.Status == AtcCommandStatus.Pending.ToString())
            .ExecuteUpdateAsync(setters => setters.SetProperty(command => command.Status, AtcCommandStatus.Sent.ToString()), cancellationToken);
    }

    public async Task MarkPublishFailedAsync(Guid commandId, string message, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var commandParameters = await context.AtcCommands
            .Where(item => item.CommandId == commandId && item.Status == AtcCommandStatus.Pending.ToString())
            .Select(item => item.Parameters)
            .SingleOrDefaultAsync(cancellationToken);
        if (commandParameters is null)
        {
            return;
        }

        var parameters = DeserializeParameters(commandParameters);
        await context.AtcCommands
            .Where(item => item.CommandId == commandId && item.Status == AtcCommandStatus.Pending.ToString())
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, AtcCommandStatus.Failed.ToString())
                .SetProperty(item => item.Parameters,
                    SerializeParameters(parameters.AircraftId, parameters.Value, parameters.ExpiresAtUtc, message)),
                cancellationToken);
    }

    public async Task<CommandResultPersistenceOutcome> ApplyResultAsync(
        AircraftCommandResult result,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var command = await context.AtcCommands.SingleOrDefaultAsync(
            item => item.CommandId == result.CommandId,
            cancellationToken);
        if (command is null)
        {
            return CommandResultPersistenceOutcome.NotFound;
        }

        var parameters = DeserializeParameters(command.Parameters);
        if (parameters.AircraftId != result.AircraftId)
        {
            return CommandResultPersistenceOutcome.AircraftMismatch;
        }

        if (command.FlightId != result.FlightId)
        {
            return CommandResultPersistenceOutcome.FlightMismatch;
        }

        var newStatus = result.Status == AircraftCommandResultStatus.Applied
            ? AtcCommandStatus.Acknowledged
            : AtcCommandStatus.Failed;
        var updated = await context.AtcCommands
            .Where(item => item.CommandId == result.CommandId
                && (item.Status == AtcCommandStatus.Pending.ToString() || item.Status == AtcCommandStatus.Sent.ToString()))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, newStatus.ToString())
                .SetProperty(item => item.Parameters,
                    SerializeParameters(parameters.AircraftId, parameters.Value, parameters.ExpiresAtUtc, result.Message)),
                cancellationToken);

        return updated > 0
            ? CommandResultPersistenceOutcome.Applied
            : CommandResultPersistenceOutcome.AlreadyFinal;
    }

    public async Task<AtcCommandRecord?> GetByCommandIdAsync(Guid commandId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.AtcCommands.AsNoTracking()
            .SingleOrDefaultAsync(command => command.CommandId == commandId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<AtcCommandRecord>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var commands = await context.AtcCommands.AsNoTracking()
            .Where(command => command.FlightId == flightId && command.CommandId != null)
            .OrderByDescending(command => command.IssuedAt)
            .ToListAsync(cancellationToken);
        return commands.Select(ToRecord).ToArray();
    }

    private static AtcCommandRecord ToRecord(AtcCommand command)
    {
        var parameters = DeserializeParameters(command.Parameters);
        if (!Enum.TryParse<AircraftCommandType>(command.CommandType, out var type))
        {
            throw new InvalidOperationException($"Unknown persisted command type '{command.CommandType}'.");
        }

        if (!Enum.TryParse<AtcCommandStatus>(command.Status, out var status))
        {
            throw new InvalidOperationException($"Unknown persisted command status '{command.Status}'.");
        }

        return new AtcCommandRecord(
            command.CommandId!.Value,
            command.FlightId,
            parameters.AircraftId,
            type,
            parameters.Value,
            DateTime.SpecifyKind(command.IssuedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(parameters.ExpiresAtUtc, DateTimeKind.Utc),
            status,
            parameters.ResultMessage);
    }

    private static string SerializeParameters(int aircraftId, double value, DateTime expiresAtUtc, string? resultMessage = null) =>
        JsonSerializer.Serialize(new CommandParameters(aircraftId, value, expiresAtUtc, resultMessage), ParameterJsonOptions);

    private static CommandParameters DeserializeParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CommandParameters(0, 0, DateTime.MinValue, null);
        }

        try
        {
            return JsonSerializer.Deserialize<CommandParameters>(json, ParameterJsonOptions)
                ?? new CommandParameters(0, 0, DateTime.MinValue, null);
        }
        catch (JsonException)
        {
            return new CommandParameters(0, 0, DateTime.MinValue, null);
        }
    }

    private sealed record CommandParameters(int AircraftId, double Value, DateTime ExpiresAtUtc, string? ResultMessage);
}
