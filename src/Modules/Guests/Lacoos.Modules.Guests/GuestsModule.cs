using Lacoos.Modules.Guests.Application;
using Lacoos.Modules.Guests.Endpoints;
using Lacoos.Modules.Guests.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Guests;

public sealed class GuestsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<GuestsDbContext>(configuration, GuestsDbContext.SchemaName);
        services.AddScoped<GuestService>();
        services.AddScoped<IGuestQueries, GuestQueries>();
        services.AddScoped<IModuleSeeder, GuestsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => GuestsEndpoints.Map(app);
}
