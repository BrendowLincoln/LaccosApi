using Lacoos.Modules.Budget.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Budget.Endpoints;

internal static class BudgetEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var budget = app.MapWeddingGroup("Orçamento").MapGroup("/budget");

        budget.MapGet("", (BudgetService service, CancellationToken ct) => service.GetOverviewAsync(ct));

        budget.MapPut("", (SetTotalBudgetRequest request, BudgetService service, CancellationToken ct) =>
            service.SetTotalAsync(request, ct));

        budget.MapGet("/by-category", (BudgetService service, CancellationToken ct) => service.GetByCategoryAsync(ct));

        var categories = budget.MapGroup("/categories");

        categories.MapGet("", (BudgetService service, CancellationToken ct) => service.ListCategoriesAsync(ct));

        categories.MapPost("", async (SaveCategoryRequest request, BudgetService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateCategoryAsync(request, ct)));

        categories.MapGet("/{id:guid}", (Guid id, BudgetService service, CancellationToken ct) => service.GetCategoryAsync(id, ct));

        categories.MapPut("/{id:guid}", (Guid id, SaveCategoryRequest request, BudgetService service, CancellationToken ct) =>
            service.UpdateCategoryAsync(id, request, ct));

        categories.MapDelete("/{id:guid}", async (Guid id, BudgetService service, CancellationToken ct) =>
        {
            await service.DeleteCategoryAsync(id, ct);
            return TypedResults.NoContent();
        });

        var items = budget.MapGroup("/items");

        items.MapGet("", ([AsParameters] BudgetItemFilter filter, BudgetService service, CancellationToken ct) =>
            service.ListItemsAsync(filter, ct));

        items.MapPost("", async (SaveItemRequest request, BudgetService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateItemAsync(request, ct)));

        items.MapGet("/{id:guid}", (Guid id, BudgetService service, CancellationToken ct) => service.GetItemAsync(id, ct));

        items.MapPut("/{id:guid}", (Guid id, SaveItemRequest request, BudgetService service, CancellationToken ct) =>
            service.UpdateItemAsync(id, request, ct));

        items.MapDelete("/{id:guid}", async (Guid id, BudgetService service, CancellationToken ct) =>
        {
            await service.DeleteItemAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
