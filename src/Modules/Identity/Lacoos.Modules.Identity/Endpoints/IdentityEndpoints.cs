using System.Security.Claims;
using Lacoos.Modules.Identity.Application;
using Lacoos.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Lacoos.Modules.Identity.Endpoints;

internal static class IdentityEndpoints
{
    public const string AuthRateLimitPolicy = "auth";

    public static void Map(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Autenticação").RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapPost("/register", async (RegisterRequest request, AccountService service, HttpContext http, IOptions<AuthOptions> options, CancellationToken ct) =>
        {
            var (user, session) = await service.RegisterAsync(request, ct);
            SessionCookies.Write(http, session, options.Value.SecureCookies);
            return TypedResults.Created("/api/me", user);
        });

        auth.MapPost("/login", async (LoginRequest request, AccountService service, HttpContext http, IOptions<AuthOptions> options, CancellationToken ct) =>
        {
            var (user, session) = await service.LoginAsync(request, ct);
            SessionCookies.Write(http, session, options.Value.SecureCookies);
            return TypedResults.Ok(user);
        });

        auth.MapPost("/logout", async (AccountService service, HttpContext http, IOptions<AuthOptions> options, CancellationToken ct) =>
        {
            await service.LogoutAsync(SessionId(http), ct);
            SessionCookies.Clear(http, options.Value.SecureCookies);
            return TypedResults.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, AccountService service, CancellationToken ct) =>
        {
            await service.ForgotPasswordAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        });

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, AccountService service, CancellationToken ct) =>
        {
            await service.ResetPasswordAsync(request, ct);
            return TypedResults.NoContent();
        });

        var me = app.MapGroup("/api/me").WithTags("Perfil").RequireAuthorization();

        me.MapGet("", (AccountService service, ICurrentUser user, CancellationToken ct) =>
            service.GetProfileAsync(user.RequiredUserId, ct));

        me.MapPatch("", (UpdateProfileRequest request, AccountService service, ICurrentUser user, CancellationToken ct) =>
            service.UpdateProfileAsync(user.RequiredUserId, request, ct));

        me.MapPost("/change-password", async (ChangePasswordRequest request, AccountService service, ICurrentUser user, HttpContext http, CancellationToken ct) =>
        {
            await service.ChangePasswordAsync(user.RequiredUserId, SessionId(http), request, ct);
            return TypedResults.NoContent();
        });
    }

    private static Guid SessionId(HttpContext http) =>
        Guid.Parse(http.User.FindFirstValue(SessionCookies.SessionIdClaim)!);
}
