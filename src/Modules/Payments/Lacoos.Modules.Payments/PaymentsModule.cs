using Lacoos.Modules.Payments.Application;
using Lacoos.Modules.Payments.Endpoints;
using Lacoos.Modules.Payments.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Payments;

public sealed class PaymentsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentsDbContext>(configuration, PaymentsDbContext.SchemaName);
        services.AddScoped<PaymentService>();
        services.AddScoped<IPaymentQueries, PaymentQueries>();
        services.AddScoped<IModuleSeeder, PaymentsSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => PaymentsEndpoints.Map(app);
}
