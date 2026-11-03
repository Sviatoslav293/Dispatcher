using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using DispatcherServer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DispatcherServer.Tests;

public sealed class AircraftCommandApiTests : IClassFixture<DispatcherApiFactory>
{
    private readonly HttpClient _client;
    private readonly InMemoryAtcCommandRepository _repository;
    private readonly FakeAircraftCommandPublisher _publisher;

    public AircraftCommandApiTests(DispatcherApiFactory factory)
    {
        _client = factory.CreateClient();
        _repository = factory.Services.GetRequiredService<InMemoryAtcCommandRepository>();
        _publisher = factory.Services.GetRequiredService<FakeAircraftCommandPublisher>();
    }

    [Theory]
    [InlineData("SetHeading", 90)]
    [InlineData("SetAltitude", 12000)]
    [InlineData("SetSpeed", 450)]
    public async Task PostCommand_AcceptsSupportedCommandTypes(string type, double value)
    {
        var response = await _client.PostAsJsonAsync("/api/flights/1/commands", new { type, value });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CommandAcceptedResponse>();
        Assert.NotNull(body);
        var stored = await _repository.GetByCommandIdAsync(body.CommandId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(type, stored.Type.ToString());
        Assert.Equal(AtcCommandStatus.Sent, stored.Status);
        Assert.Contains(_publisher.PublishedCommands, command => command.CommandId == body.CommandId);
    }

    [Theory]
    [InlineData("SetHeading", 360)]
    [InlineData("SetAltitude", -1)]
    [InlineData("Unknown", 50)]
    [InlineData("0", 50)]
    public async Task PostCommand_InvalidParameters_ReturnsBadRequest(string type, double value)
    {
        var response = await _client.PostAsJsonAsync("/api/flights/1/commands", new { type, value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<ApiMessage>());
    }

    [Fact]
    public async Task PostCommand_UnknownFlight_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/flights/404/commands", new { type = "SetHeading", value = 90 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCommand_InactiveFlight_ReturnsConflict()
    {
        var response = await _client.PostAsJsonAsync("/api/flights/2/commands", new { type = "SetHeading", value = 90 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCommand_PublishFailure_ReturnsServiceUnavailableAndPersistsFailure()
    {
        _publisher.Failure = new InvalidOperationException("test broker failure");
        try
        {
            var response = await _client.PostAsJsonAsync("/api/flights/1/commands", new { type = "SetSpeed", value = 300 });
            var body = await response.Content.ReadFromJsonAsync<CommandAcceptedResponse>();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.NotNull(body);
            var stored = await _repository.GetByCommandIdAsync(body.CommandId, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.Equal(AtcCommandStatus.Failed, stored.Status);
        }
        finally
        {
            _publisher.Failure = null;
        }
    }

    [Fact]
    public async Task GetCommands_ReturnsCreatedCommandAndStatus()
    {
        var accepted = await _client.PostAsJsonAsync("/api/flights/1/commands", new { type = "SetHeading", value = 180 });
        var acceptedBody = await accepted.Content.ReadFromJsonAsync<CommandAcceptedResponse>();

        var response = await _client.GetAsync($"/api/commands/{acceptedBody!.CommandId}");
        var command = await response.Content.ReadFromJsonAsync<AircraftCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(command);
        Assert.Equal("SetHeading", command.Type);
        Assert.Equal("Sent", command.Status);
        Assert.Equal(180, command.Value);
        Assert.NotEqual(default, command.IssuedAtUtc);
    }

    private sealed record CommandAcceptedResponse(Guid CommandId);
    private sealed record ApiMessage(string Message);
}

public sealed class AircraftCommandContractTests
{
    [Fact]
    public void Publisher_UsesSimulatorCompatibleJsonAndRoutingKey()
    {
        var issuedAt = new DateTime(2026, 8, 2, 10, 30, 0, DateTimeKind.Utc);
        var command = new AircraftCommand(
            Guid.Parse("d8ab25e8-f13b-4b79-8f81-6fc076ea2b00"),
            34,
            87,
            AircraftCommandType.SetHeading,
            90,
            issuedAt,
            issuedAt.AddMinutes(2));

        using var document = JsonDocument.Parse(RabbitMqAircraftCommandPublisher.Serialize(command));
        var root = document.RootElement;

        Assert.Equal("aircraft.34.command", RabbitMqAircraftCommandPublisher.GetRoutingKey(34));
        Assert.Equal(command.CommandId, root.GetProperty("commandId").GetGuid());
        Assert.Equal(34, root.GetProperty("aircraftId").GetInt32());
        Assert.Equal(87, root.GetProperty("flightId").GetInt32());
        Assert.Equal("setHeading", root.GetProperty("type").GetString());
        Assert.Equal(90, root.GetProperty("value").GetDouble());
        Assert.EndsWith("Z", root.GetProperty("issuedAtUtc").GetString());
        Assert.EndsWith("Z", root.GetProperty("expiresAtUtc").GetString());
    }

    [Theory]
    [InlineData(AircraftCommandResultStatus.Applied, AtcCommandStatus.Acknowledged)]
    [InlineData(AircraftCommandResultStatus.Rejected, AtcCommandStatus.Failed)]
    public async Task Results_TransitionCommandFromPendingToSentThenFinal(
        AircraftCommandResultStatus resultStatus,
        AtcCommandStatus expectedStatus)
    {
        var repository = new InMemoryAtcCommandRepository();
        var command = CreateCommand();
        await repository.CreatePendingAsync(command, CancellationToken.None);
        await repository.MarkSentAsync(command.CommandId, CancellationToken.None);
        var outcome = await repository.ApplyResultAsync(CreateResult(command, resultStatus), CancellationToken.None);

        var stored = await repository.GetByCommandIdAsync(command.CommandId, CancellationToken.None);
        Assert.Equal(CommandResultPersistenceOutcome.Applied, outcome);
        Assert.Equal(expectedStatus, stored!.Status);
    }

    [Fact]
    public async Task DuplicateResult_IsIdempotent()
    {
        var repository = new InMemoryAtcCommandRepository();
        var command = CreateCommand();
        await repository.CreatePendingAsync(command, CancellationToken.None);
        var result = CreateResult(command, AircraftCommandResultStatus.Applied);

        Assert.Equal(CommandResultPersistenceOutcome.Applied,
            await repository.ApplyResultAsync(result, CancellationToken.None));
        Assert.Equal(CommandResultPersistenceOutcome.AlreadyFinal,
            await repository.ApplyResultAsync(result, CancellationToken.None));
        Assert.Equal(AtcCommandStatus.Acknowledged,
            (await repository.GetByCommandIdAsync(command.CommandId, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task ResultWithMismatchedFlightId_IsRejected()
    {
        var repository = new InMemoryAtcCommandRepository();
        var command = CreateCommand();
        await repository.CreatePendingAsync(command, CancellationToken.None);
        var result = CreateResult(command, AircraftCommandResultStatus.Applied) with { FlightId = command.FlightId + 1 };

        Assert.Equal(CommandResultPersistenceOutcome.FlightMismatch,
            await repository.ApplyResultAsync(result, CancellationToken.None));
        Assert.Equal(AtcCommandStatus.Pending,
            (await repository.GetByCommandIdAsync(command.CommandId, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task ResultArrivingBeforeSent_CannotBeOverwrittenBySentUpdate()
    {
        var repository = new InMemoryAtcCommandRepository();
        var command = CreateCommand();
        await repository.CreatePendingAsync(command, CancellationToken.None);

        await repository.ApplyResultAsync(CreateResult(command, AircraftCommandResultStatus.Applied), CancellationToken.None);
        await repository.MarkSentAsync(command.CommandId, CancellationToken.None);

        Assert.Equal(AtcCommandStatus.Acknowledged,
            (await repository.GetByCommandIdAsync(command.CommandId, CancellationToken.None))!.Status);
    }

    private static AircraftCommand CreateCommand()
    {
        var now = DateTime.UtcNow;
        return new AircraftCommand(Guid.NewGuid(), 34, 87, AircraftCommandType.SetHeading, 90, now, now.AddMinutes(2));
    }

    private static AircraftCommandResult CreateResult(AircraftCommand command, AircraftCommandResultStatus status) => new(
        command.CommandId,
        command.AircraftId,
        command.FlightId,
        status,
        "Результат тестовий",
        DateTime.UtcNow);
}
