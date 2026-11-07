using Lacoos.Modules.Weddings.Application;
using Lacoos.Modules.Weddings.Endpoints;
using Lacoos.Modules.Weddings.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Weddings;

public sealed class WeddingsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<WeddingsDbContext>(configuration, WeddingsDbContext.SchemaName);
        services.AddScoped<WeddingService>();
        services.AddScoped<MemberService>();
        services.AddScoped<IWeddingMembershipResolver, MembershipResolver>();
        services.AddScoped<IWeddingDirectory, WeddingDirectory>();
        services.AddScoped<IModuleSeeder, WeddingsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => WeddingsEndpoints.Map(app);
}
