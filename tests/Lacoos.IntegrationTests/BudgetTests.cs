using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class BudgetTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Total_falls_back_to_the_sum_of_categories_until_defined()
    {
        var (owner, w, _) = await NewOwner();
        await owner.Post($"{w}/budget/categories", new { name = "A", plannedAmount = 1000 });
        await owner.Post($"{w}/budget/categories", new { name = "B", plannedAmount = 500.50 });

        var fallback = await owner.Get($"{w}/budget");
        var defined = await owner.Put($"{w}/budget", new { totalBudget = 2000 });
        var redefined = await owner.Put($"{w}/budget", new { totalBudget = 3000 });

        Assert.Equal(1500.50m, fallback.Json.Dec("totalBudget"));
        Assert.False(fallback.Json.At("totalBudgetDefined").GetBoolean());
        Assert.Equal(2000, defined.Json.Dec("totalBudget"));
        Assert.True(defined.Json.At("totalBudgetDefined").GetBoolean());
        Assert.Equal(3000, redefined.Json.Dec("totalBudget"));
    }

    [Fact]
    public async Task Name_and_category_are_required_and_values_cannot_be_negative()
    {
        var (owner, w, _) = await NewOwner();
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();

        var noName = await owner.Post($"{w}/budget/items", new { categoryId = category });
        var noCategory = await owner.Post($"{w}/budget/items", new { name = "X" });
        var negative = await owner.Post($"{w}/budget/items", new { name = "X", categoryId = category, plannedAmount = -1 });
        var negativeCategory = await owner.Post($"{w}/budget/categories", new { name = "B", plannedAmount = -1 });
        var tooPrecise = await owner.Post($"{w}/budget/items", new { name = "X", categoryId = category, contractedAmount = 10.123 });

        Assert.Equal(HttpStatusCode.BadRequest, noName.Status);
        Assert.Equal(HttpStatusCode.BadRequest, noCategory.Status);
        Assert.Equal("Valor planejado não pode ser negativo.", negative.Json.Str("errors.plannedAmount.0"));
        Assert.Equal(HttpStatusCode.BadRequest, negativeCategory.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooPrecise.Status);
    }

    [Fact]
    public async Task Category_with_items_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();
        var item = (await owner.Post($"{w}/budget/items", new { name = "X", categoryId = category })).Json.Id();

        var blocked = await owner.Delete($"{w}/budget/categories/{category}");
        await owner.Delete($"{w}/budget/items/{item}");
        var allowed = await owner.Delete($"{w}/budget/categories/{category}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    [Fact]
    public async Task Item_with_payments_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();
        var item = (await owner.Post($"{w}/budget/items", new { name = "X", categoryId = category })).Json.Id();
        var payment = await owner.Post($"{w}/payments", new { description = "P", totalAmount = 10, paymentMethod = "pix", firstDueDate = "2030-01-01", budgetItemId = item });

        var blocked = await owner.Delete($"{w}/budget/items/{item}");
        await owner.Delete($"{w}/payments/{payment.Json.Id()}");
        var allowed = await owner.Delete($"{w}/budget/items/{item}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    [Fact]
    public async Task Totals_ignore_cancelled_items_and_paid_comes_from_linked_payments()
    {
        var (owner, w, _) = await NewOwner();
        await owner.Put($"{w}/budget", new { totalBudget = 10000 });
        var category = (await owner.Post($"{w}/budget/categories", new { name = "Festa", plannedAmount = 8000 })).Json.Id();
        var active = (await owner.Post($"{w}/budget/items", new { name = "Ativo", categoryId = category, plannedAmount = 3000, contractedAmount = 2500, status = "contracted" })).Json.Id();
        await owner.Post($"{w}/budget/items", new { name = "Cancelado", categoryId = category, plannedAmount = 2000, contractedAmount = 1500, status = "cancelled" });

        var linked = await owner.Post($"{w}/payments", new { description = "Vinculado", totalAmount = 2500, paymentMethod = "pix", firstDueDate = "2030-01-01", budgetItemId = active });
        await owner.Post($"{w}/installments/{linked.Json.Id("installments.0.id")}/settlements", new { amount = 1000 });
        var loose = await owner.Post($"{w}/payments", new { description = "Solto", totalAmount = 700, paymentMethod = "pix", firstDueDate = "2030-01-01" });
        await owner.Post($"{w}/installments/{loose.Json.Id("installments.0.id")}/settlements", new { amount = 700 });

        var overview = await owner.Get($"{w}/budget");
        var items = await owner.Get($"{w}/budget/items?search=ativo");
        var chart = await owner.Get($"{w}/budget/by-category");
        var dashboard = await owner.Get($"{w}/dashboard");

        Assert.Equal(8000, overview.Json.Dec("totals.planned"));
        Assert.Equal(2500, overview.Json.Dec("totals.contracted"));
        Assert.Equal(1000, overview.Json.Dec("totals.paid"));
        Assert.Equal(1500, overview.Json.Dec("totals.pending"));
        Assert.Equal(7500, overview.Json.Dec("totals.balance"));
        Assert.Equal(25, overview.Json.Dec("totals.committedPercent"));
        Assert.Equal(1000, items.Json.Dec("0.paidAmount"));
        Assert.Equal(2500, items.Json.Dec("0.contractedAmount"));
        Assert.Equal(3000, items.Json.Dec("0.plannedAmount"));
        Assert.Equal(8000, chart.Json.Dec("0.planned"));
        Assert.Equal(2500, chart.Json.Dec("0.contracted"));
        Assert.Equal(1700, dashboard.Json.Dec("financial.paid"));
    }

    [Fact]
    public async Task Items_can_be_filtered_by_category_supplier_and_status()
    {
        var (owner, w, _) = await NewOwner();
        var a = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();
        var b = (await owner.Post($"{w}/budget/categories", new { name = "B" })).Json.Id();
        var supplier = (await owner.Post($"{w}/suppliers", new { name = "Foto", category = "Fotografia" })).Json.Id("supplier.id");
        await owner.Post($"{w}/budget/items", new { name = "Um", categoryId = a, supplierId = supplier, status = "contracted" });
        await owner.Post($"{w}/budget/items", new { name = "Dois", categoryId = b });

        var byCategory = await owner.Get($"{w}/budget/items?categoryId={b}");
        var bySupplier = await owner.Get($"{w}/budget/items?supplierId={supplier}");
        var byStatus = await owner.Get($"{w}/budget/items?status=contracted");

        Assert.Equal("Dois", byCategory.Json.Str("0.name"));
        Assert.Equal("Foto", bySupplier.Json.Str("0.supplierName"));
        Assert.Equal(1, byStatus.Json.Count());
    }
}
