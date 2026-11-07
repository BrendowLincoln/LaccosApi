using Lacoos.Modules.Weddings.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Weddings.Endpoints;

internal static class WeddingsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/weddings").WithTags("Casamentos").RequireAuthorization();

        root.MapPost("", async (CreateWeddingRequest request, WeddingService service, CancellationToken ct) =>
        {
            var wedding = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/weddings/{wedding.Id}", wedding);
        });

        root.MapGet("", (WeddingService service, CancellationToken ct) => service.ListMineAsync(ct));

        var w = app.MapWeddingGroup("Casamentos");

        w.MapGet("", (WeddingService service, CancellationToken ct) => service.GetCurrentAsync(ct));

        w.MapPut("", (UpdateWeddingRequest request, WeddingService service, CancellationToken ct) =>
            service.UpdateAsync(request, ct)).RequireRole(WeddingRole.Owner);

        w.MapPatch("/status", (ChangeStatusRequest request, WeddingService service, CancellationToken ct) =>
            service.ChangeStatusAsync(request, ct)).RequireRole(WeddingRole.Owner);

        var members = w.MapGroup("/members").WithTags("Membros");

        members.MapGet("", (MemberService service, CancellationToken ct) => service.ListAsync(ct));

        members.MapPost("", async (InviteMemberRequest request, MemberService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.InviteAsync(request, ct))).RequireRole(WeddingRole.Owner);

        members.MapPost("/{memberId:guid}/resend-invite", (Guid memberId, MemberService service, CancellationToken ct) =>
            service.ResendInviteAsync(memberId, ct)).RequireRole(WeddingRole.Owner);

        members.MapPatch("/{memberId:guid}", (Guid memberId, UpdateMemberRequest request, MemberService service, CancellationToken ct) =>
            service.UpdateAsync(memberId, request, ct)).RequireRole(WeddingRole.Owner);

        members.MapDelete("/{memberId:guid}", async (Guid memberId, MemberService service, CancellationToken ct) =>
        {
            await service.RemoveAsync(memberId, ct);
            return TypedResults.NoContent();
        }).RequireRole(WeddingRole.Owner);

        var invitations = app.MapGroup("/api/invitations").WithTags("Convites");

        invitations.MapGet("/{token}", (string token, MemberService service, CancellationToken ct) =>
            service.PreviewInvitationAsync(token, ct));

        invitations.MapPost("/{token}/accept", (string token, MemberService service, CancellationToken ct) =>
            service.AcceptInvitationAsync(token, ct)).RequireAuthorization();
    }
}
