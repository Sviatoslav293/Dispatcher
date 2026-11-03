using DispatcherServer.Core.Persistence;
using DispatcherServer.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DispatcherServer.Api;
using DispatcherServer.Plugins;

namespace DispatcherServer.Tests;

public sealed class DispatcherApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RabbitMq:UserName", "test");
        builder.UseSetting("RabbitMq:Password", "test");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IFlightRepository>();
            services.RemoveAll<IRouteRepository>();
            services.AddSingleton<IFlightRepository, FakeFlightRepository>();
            services.AddSingleton<IRouteRepository, FakeRouteRepository>();
            services.AddSingleton<InMemoryAtcCommandRepository>();
            services.AddSingleton<IAtcCommandRepository>(provider => provider.GetRequiredService<InMemoryAtcCommandRepository>());
            services.AddSingleton<FakeAircraftCommandPublisher>();
            services.AddSingleton<IAircraftCommandPublisher>(provider => provider.GetRequiredService<FakeAircraftCommandPublisher>());
            services.AddSingleton<IServerModule, AircraftCommandModule>();
            services.AddHostedService<ServerMicrokernelHostedService>();
        });
    }

}
