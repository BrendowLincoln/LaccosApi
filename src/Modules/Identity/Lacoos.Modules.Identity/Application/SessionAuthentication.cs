using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Lacoos.Modules.Identity.Persistence;
using Lacoos.SharedKernel.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lacoos.Modules.Identity.Application;

internal static class SessionCookies
{
    public const string Scheme = "LacoosSession";
    public const string SessionCookie = "lacoos_session";
    public const string CsrfCookie = "XSRF-TOKEN";
    public const string CsrfHeader = "X-XSRF-TOKEN";
    public const string SessionIdClaim = "sid";
    public const string CsrfClaim = "csrf";

    public static void Write(HttpContext http, IssuedSession session, bool secure)
    {
        http.Response.Cookies.Append(SessionCookie, session.Token, SessionOptions(session.ExpiresAt, secure));
        WriteCsrf(http, session.CsrfToken, session.ExpiresAt, secure);
    }

    /// <summary>Legível pelo JavaScript: o Angular copia este valor para o cabeçalho X-XSRF-TOKEN.</summary>
    public static void WriteCsrf(HttpContext http, string csrf, DateTimeOffset expiresAt, bool secure) =>
        http.Response.Cookies.Append(CsrfCookie, csrf, new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
        });

    public static void Clear(HttpContext http, bool secure)
    {
        http.Response.Cookies.Delete(SessionCookie, SessionOptions(null, secure));
        http.Response.Cookies.Delete(CsrfCookie, new CookieOptions { Path = "/", Secure = secure, SameSite = SameSiteMode.Lax });
    }

    private static CookieOptions SessionOptions(DateTimeOffset? expiresAt, bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expiresAt,
    };
}

/// <summary>Autentica pelo cookie de sessão. A sessão fica no banco, então o logout é imediato.</summary>
internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IdentityDbContext db,
    IClock clock,
    IOptions<AuthOptions> authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionCookies.SessionCookie, out var token) || string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = Tokens.Hash(token);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash, Context.RequestAborted);
        var now = clock.UtcNow;
        if (session is null || session.ExpiresAt <= now)
        {
            return AuthenticateResult.NoResult();
        }

        // Renova a validade no máximo uma vez por hora, para não escrever no banco a cada requisição.
        if (now - session.LastSeenAt > TimeSpan.FromHours(1))
        {
            session.LastSeenAt = now;
            session.ExpiresAt = now.AddDays(authOptions.Value.SessionDays);
            await db.SaveChangesAsync(Context.RequestAborted);
            SessionCookies.Write(Context, new IssuedSession(token, session.CsrfToken, session.ExpiresAt), authOptions.Value.SecureCookies);
        }
        else if (!Request.Cookies.ContainsKey(SessionCookies.CsrfCookie))
        {
            SessionCookies.WriteCsrf(Context, session.CsrfToken, session.ExpiresAt, authOptions.Value.SecureCookies);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(SessionCookies.SessionIdClaim, session.Id.ToString()),
                new Claim(SessionCookies.CsrfClaim, session.CsrfToken),
            ],
            SessionCookies.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SessionCookies.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Problems.WriteAsync(Context, StatusCodes.Status401Unauthorized, "Autenticação necessária.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Problems.WriteAsync(Context, StatusCodes.Status403Forbidden, "Acesso negado.");
}

/// <summary>Em requisições de escrita autenticadas, exige o token CSRF da sessão no cabeçalho.</summary>
internal sealed class CsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var method = http.Request.Method;
        var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        if (!safe && http.User.Identity?.IsAuthenticated == true)
        {
            var expected = http.User.FindFirstValue(SessionCookies.CsrfClaim) ?? string.Empty;
            var received = http.Request.Headers[SessionCookies.CsrfHeader].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received)))
            {
                await Problems.WriteAsync(http, StatusCodes.Status403Forbidden, "Token CSRF ausente ou inválido.");
                return;
            }
        }

        await next(http);
    }
}
