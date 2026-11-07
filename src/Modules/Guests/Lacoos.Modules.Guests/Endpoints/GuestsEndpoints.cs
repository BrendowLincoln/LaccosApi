using Lacoos.Modules.Guests.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Guests.Endpoints;

internal static class GuestsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var w = app.MapWeddingGroup("Convidados");
        var guests = w.MapGroup("/guests");

        guests.MapGet("", ([AsParameters] GuestFilter filter, GuestService service, CancellationToken ct) =>
            service.ListAsync(filter, ct));

        guests.MapGet("/summary", (GuestService service, CancellationToken ct) => service.GetSummaryAsync(ct));

        guests.MapPost("", async (SaveGuestRequest request, GuestService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)));

        guests.MapGet("/{id:guid}", (Guid id, GuestService service, CancellationToken ct) => service.GetAsync(id, ct));

        guests.MapPut("/{id:guid}", (Guid id, SaveGuestRequest request, GuestService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        guests.MapDelete("/{id:guid}", async (Guid id, GuestService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        guests.MapPost("/{id:guid}/mark-sent", (Guid id, GuestService service, CancellationToken ct) =>
            service.MarkSentAsync(id, ct));

        var groups = w.MapGroup("/guest-groups");

        groups.MapGet("", (GuestService service, CancellationToken ct) => service.ListGroupsAsync(ct));

        groups.MapPost("", async (SaveGroupRequest request, GuestService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateGroupAsync(request, ct)));

        groups.MapGet("/{id:guid}", (Guid id, GuestService service, CancellationToken ct) => service.GetGroupAsync(id, ct));

        groups.MapPut("/{id:guid}", (Guid id, SaveGroupRequest request, GuestService service, CancellationToken ct) =>
            service.UpdateGroupAsync(id, request, ct));

        groups.MapDelete("/{id:guid}", async (Guid id, GuestService service, CancellationToken ct) =>
        {
            await service.DeleteGroupAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
