using DispatcherServer.Core.Persistence;
using DispatcherServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherServer.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddDispatcherPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<DispatcherDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DispatcherDbContext>(provider => provider.GetRequiredService<IDbContextFactory<DispatcherDbContext>>().CreateDbContext());
        services.AddScoped<IAircraftRepository, AircraftRepository>();
        services.AddScoped<IFlightRepository, FlightRepository>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddSingleton<IAtcCommandRepository, AtcCommandRepository>();
        services.AddSingleton<IAircraftPositionPersistence, AircraftPositionPersistence>();
        services.AddSingleton<IFlightStatusPersistence, FlightStatusPersistence>();
        return services;
    }
}
