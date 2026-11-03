using DispatcherServer.Persistence;
using DispatcherServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DispatcherServer.Tests;

internal sealed class PersistenceTestFixture : IAsyncDisposable
{
    private readonly string _databaseName = $"DispatcherTests-{Guid.NewGuid():N}";
    private readonly DbContextOptions<DispatcherDbContext> _options;
    private readonly TestContextFactory _contextFactory;

    public PersistenceTestFixture()
    {
        _options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        _contextFactory = new TestContextFactory(_options);
    }

    public IDbContextFactory<DispatcherDbContext> ContextFactory => _contextFactory;

    public async Task SeedAsync(params int[] aircraftIds)
    {
        await using var context = new DispatcherDbContext(_options);
        foreach (var aircraftId in aircraftIds)
        {
            context.Aircraft.Add(new Aircraft
            {
                Id = aircraftId,
                RegistrationNumber = $"TEST-{aircraftId}",
                Model = "Test",
                AircraftType = "Test"
            });
        }

        await context.SaveChangesAsync();
    }

    public DispatcherDbContext CreateContext() => new(_options);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class TestContextFactory(DbContextOptions<DispatcherDbContext> options)
        : IDbContextFactory<DispatcherDbContext>
    {
        public DispatcherDbContext CreateDbContext() => new(options);
    }
}
