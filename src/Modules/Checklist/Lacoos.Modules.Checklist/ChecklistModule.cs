using Lacoos.Modules.Checklist.Application;
using Lacoos.Modules.Checklist.Endpoints;
using Lacoos.Modules.Checklist.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Checklist;

public sealed class ChecklistModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ChecklistDbContext>(configuration, ChecklistDbContext.SchemaName);
        services.AddScoped<ChecklistService>();
        services.AddScoped<IChecklistQueries, ChecklistQueries>();
        services.AddScoped<IChecklistAssignments, ChecklistQueries>();
        services.AddScoped<IModuleSeeder, ChecklistSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => ChecklistEndpoints.Map(app);
}
