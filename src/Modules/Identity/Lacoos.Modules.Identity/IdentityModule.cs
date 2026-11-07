using System.Threading.RateLimiting;
using Lacoos.Modules.Identity.Application;
using Lacoos.Modules.Identity.Domain;
using Lacoos.Modules.Identity.Endpoints;
using Lacoos.Modules.Identity.Persistence;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.SchemaName);
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<AccountService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IModuleSeeder, IdentitySeeder>();

        services.AddAuthentication(SessionCookies.Scheme)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionCookies.Scheme, null);
        services.AddAuthorization();

        var permitPerMinute = configuration.GetValue("Auth:RateLimitPerMinute", 20);
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(IdentityEndpoints.AuthRateLimitPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => IdentityEndpoints.Map(app);

    public void ConfigurePipeline(IApplicationBuilder app) => app.UseMiddleware<CsrfMiddleware>();
}
