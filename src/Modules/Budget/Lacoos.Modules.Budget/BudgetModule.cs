using Lacoos.Modules.Budget.Application;
using Lacoos.Modules.Budget.Endpoints;
using Lacoos.Modules.Budget.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Budget;

public sealed class BudgetModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<BudgetDbContext>(configuration, BudgetDbContext.SchemaName);
        services.AddScoped<BudgetService>();
        services.AddScoped<IBudgetQueries, BudgetQueries>();
        services.AddScoped<IModuleSeeder, BudgetSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => BudgetEndpoints.Map(app);
}
