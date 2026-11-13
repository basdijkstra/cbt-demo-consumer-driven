using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerApi.Tests;

/// <summary>
/// Hosts the real API on a Kestrel socket (the Pact verifier needs a real HTTP
/// endpoint) and puts the provider state endpoint in front of the API pipeline.
/// </summary>
public class CustomerApiFactory : WebApplicationFactory<Program>
{
    public CustomerApiFactory()
    {
        UseKestrel(0);
        StartServer();
    }

    public Uri ServerUri => new(Services.GetRequiredService<IServer>()
        .Features.GetRequiredFeature<IServerAddressesFeature>()
        .Addresses.First());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            services.AddTransient<IStartupFilter, ProviderStateStartupFilter>());
    }

    private class ProviderStateStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseMiddleware<ProviderStateMiddleware>();
            next(app);
        };
    }
}
