using System.Security.Claims;
using Lacoos.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Lacoos.SharedKernel.Tenancy;

public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid RequiredUserId { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid RequiredUserId => UserId ?? throw new UnauthorizedException("Autenticação necessária.");
}
