using Lacoos.SharedKernel.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.SharedKernel.Tenancy;

public sealed record RequiredRole(WeddingRole Role);

/// <summary>
/// Aplica o papel em todas as rotas de casamento. Por padrão, leitura exige viewer e escrita
/// exige organizer; rotas que pedem mais usam <see cref="WeddingRoutes.RequireRole{T}"/>.
/// </summary>
public sealed class WeddingAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;

        if (!Guid.TryParse(http.Request.RouteValues["weddingId"]?.ToString(), out var weddingId))
        {
            throw new NotFoundException("Casamento não encontrado.");
        }

        var userId = services.GetRequiredService<ICurrentUser>().RequiredUserId;
        var role = await services.GetRequiredService<IWeddingMembershipResolver>()
            .GetActiveRoleAsync(weddingId, userId, http.RequestAborted);

        // Quem não é membro recebe 404, para não revelar que o casamento existe.
        if (role is null)
        {
            throw new NotFoundException("Casamento não encontrado.");
        }

        var required = http.GetEndpoint()?.Metadata.GetMetadata<RequiredRole>()?.Role ?? DefaultRole(http.Request.Method);
        if (role < required)
        {
            throw new ForbiddenException("Seu papel neste casamento não permite esta ação.");
        }

        services.GetRequiredService<WeddingContext>().Set(weddingId, role);
        return await next(context);
    }

    private static WeddingRole DefaultRole(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ? WeddingRole.Viewer : WeddingRole.Organizer;
}

public static class WeddingRoutes
{
    public const string Prefix = "/api/weddings/{weddingId:guid}";

    /// <summary>Grupo autenticado, com checagem de papel e isolamento por casamento.</summary>
    public static RouteGroupBuilder MapWeddingGroup(this IEndpointRouteBuilder app, string tag)
    {
        var group = app.MapGroup(Prefix);
        group.RequireAuthorization();
        group.AddEndpointFilter<WeddingAccessFilter>();
        group.WithTags(tag);
        return group;
    }

    public static T RequireRole<T>(this T builder, WeddingRole role) where T : IEndpointConventionBuilder =>
        builder.WithMetadata(new RequiredRole(role));
}
