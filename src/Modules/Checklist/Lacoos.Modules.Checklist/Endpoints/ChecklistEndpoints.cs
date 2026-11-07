using Lacoos.Modules.Checklist.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Checklist.Endpoints;

internal static class ChecklistEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var checklist = app.MapWeddingGroup("Checklist").MapGroup("/checklist");

        var items = checklist.MapGroup("/items");

        items.MapGet("", ([AsParameters] ChecklistFilter filter, ChecklistService service, CancellationToken ct) =>
            service.ListAsync(filter, ct));

        items.MapPost("", async (SaveItemRequest request, ChecklistService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)));

        items.MapGet("/{id:guid}", (Guid id, ChecklistService service, CancellationToken ct) => service.GetAsync(id, ct));

        items.MapPut("/{id:guid}", (Guid id, SaveItemRequest request, ChecklistService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        items.MapDelete("/{id:guid}", async (Guid id, ChecklistService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        var categories = checklist.MapGroup("/categories");

        categories.MapGet("", (bool? active, ChecklistService service, CancellationToken ct) =>
            service.ListCategoriesAsync(active, ct));

        categories.MapPost("", async (SaveCategoryRequest request, ChecklistService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateCategoryAsync(request, ct)));

        categories.MapPut("/{id:guid}", (Guid id, SaveCategoryRequest request, ChecklistService service, CancellationToken ct) =>
            service.UpdateCategoryAsync(id, request, ct));

        categories.MapPatch("/{id:guid}/active", (Guid id, SetActiveRequest request, ChecklistService service, CancellationToken ct) =>
            service.SetCategoryActiveAsync(id, request, ct));
    }
}
