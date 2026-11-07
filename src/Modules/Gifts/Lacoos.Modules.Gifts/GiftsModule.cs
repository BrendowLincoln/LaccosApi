using Lacoos.Modules.Gifts.Application;
using Lacoos.Modules.Gifts.Endpoints;
using Lacoos.Modules.Gifts.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Gifts;

public sealed class GiftsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<GiftsDbContext>(configuration, GiftsDbContext.SchemaName);
        services.AddScoped<GiftService>();
        services.AddScoped<IGiftGuestLinks, GiftGuestLinks>();
        services.AddScoped<IModuleSeeder, GiftsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => GiftsEndpoints.Map(app);
}
