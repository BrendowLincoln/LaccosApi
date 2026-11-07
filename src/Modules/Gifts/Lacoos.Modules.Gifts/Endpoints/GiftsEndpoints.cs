using Lacoos.Modules.Gifts.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Gifts.Endpoints;

internal static class GiftsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var w = app.MapWeddingGroup("Presentes");
        var gifts = w.MapGroup("/gifts");

        gifts.MapGet("", ([AsParameters] GiftFilter filter, GiftService service, CancellationToken ct) =>
            service.ListAsync(filter, ct));

        gifts.MapGet("/summary", (GiftService service, CancellationToken ct) => service.GetTotalsAsync(ct));

        gifts.MapPost("", async (SaveGiftRequest request, GiftService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)));

        gifts.MapGet("/{id:guid}", (Guid id, GiftService service, CancellationToken ct) => service.GetAsync(id, ct));

        gifts.MapPut("/{id:guid}", (Guid id, SaveGiftRequest request, GiftService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        gifts.MapDelete("/{id:guid}", async (Guid id, GiftService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        gifts.MapPost("/{id:guid}/cancel", (Guid id, GiftService service, CancellationToken ct) => service.CancelAsync(id, ct));

        gifts.MapPost("/{id:guid}/reactivate", (Guid id, GiftService service, CancellationToken ct) => service.ReactivateAsync(id, ct));

        gifts.MapPost("/{id:guid}/reservations", async (Guid id, SaveReservationRequest request, GiftService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.ReserveAsync(id, request, ct)));

        gifts.MapPost("/{id:guid}/receipts", async (Guid id, SaveReceiptRequest request, GiftService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.ReceiveAsync(id, request, ct)));

        w.MapPost("/gift-reservations/{id:guid}/cancel", (Guid id, GiftService service, CancellationToken ct) =>
            service.CancelReservationAsync(id, ct));

        var categories = w.MapGroup("/gift-categories");

        categories.MapGet("", (GiftService service, CancellationToken ct) => service.ListCategoriesAsync(ct));

        categories.MapPost("", async (SaveCategoryRequest request, GiftService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateCategoryAsync(request, ct)));

        categories.MapGet("/{id:guid}", (Guid id, GiftService service, CancellationToken ct) => service.GetCategoryAsync(id, ct));

        categories.MapPut("/{id:guid}", (Guid id, SaveCategoryRequest request, GiftService service, CancellationToken ct) =>
            service.UpdateCategoryAsync(id, request, ct));

        categories.MapDelete("/{id:guid}", async (Guid id, GiftService service, CancellationToken ct) =>
        {
            await service.DeleteCategoryAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
