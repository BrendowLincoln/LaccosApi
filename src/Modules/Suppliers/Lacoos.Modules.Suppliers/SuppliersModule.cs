using Lacoos.Modules.Suppliers.Application;
using Lacoos.Modules.Suppliers.Endpoints;
using Lacoos.Modules.Suppliers.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Suppliers;

public sealed class SuppliersModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SuppliersDbContext>(configuration, SuppliersDbContext.SchemaName);
        services.AddScoped<SupplierAppService>();
        services.AddScoped<ISupplierQueries, SupplierQueries>();
        services.AddScoped<IModuleSeeder, SuppliersSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => SuppliersEndpoints.Map(app);
}
