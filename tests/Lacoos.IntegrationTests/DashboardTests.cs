using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class DashboardTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Days_remaining_has_a_minimum_of_zero()
    {
        var owner = await Api.NewUser();
        Api.Clock.SetToday(new DateOnly(2030, 1, 1));
        var future = await owner.NewWedding(new DateOnly(2030, 1, 11));
        var past = await owner.NewWedding(new DateOnly(2029, 1, 1));

        Assert.Equal(10, (await owner.Get($"{W(future)}/dashboard")).Json.Int("daysRemaining"));
        Assert.Equal(0, (await owner.Get($"{W(past)}/dashboard")).Json.Int("daysRemaining"));
    }

    [Fact]
    public async Task Aggregates_checklist_financial_and_guests()
    {
        var (owner, w, _) = await NewOwner();
        await owner.Post($"{w}/checklist/items", new { title = "A", status = "done" });
        await owner.Post($"{w}/checklist/items", new { title = "B" });
        await owner.Post($"{w}/checklist/items", new { title = "C", status = "in_progress" });
        await owner.Post($"{w}/checklist/items", new { title = "D", status = "done" });
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A", plannedAmount = 4000 })).Json.Id();
        await owner.Post($"{w}/budget/items", new { name = "Item", categoryId = category, contractedAmount = 1000 });
        await owner.Post($"{w}/budget/items", new { name = "Cancelado", categoryId = category, contractedAmount = 900, status = "cancelled" });
        await owner.Post($"{w}/guests", new { firstName = "A", lastName = "B", attendanceStatus = "confirmed" });
        await owner.Post($"{w}/guests", new { firstName = "C", lastName = "D", attendanceStatus = "declined" });
        await owner.Post($"{w}/guests", new { firstName = "E", lastName = "F" });

        var dashboard = await owner.Get($"{w}/dashboard");

        Assert.Equal(HttpStatusCode.OK, dashboard.Status);
        Assert.Equal(4, dashboard.Json.Int("checklist.total"));
        Assert.Equal(2, dashboard.Json.Int("checklist.done"));
        Assert.Equal(2, dashboard.Json.Int("checklist.open"));
        Assert.Equal(50, dashboard.Json.Dec("checklist.completionPercent"));
        Assert.Equal(4000, dashboard.Json.Dec("financial.totalBudget"));
        Assert.Equal(1000, dashboard.Json.Dec("financial.committed"));
        Assert.Equal(3000, dashboard.Json.Dec("financial.balance"));
        Assert.Equal(25, dashboard.Json.Dec("financial.committedPercent"));
        Assert.Equal(3, dashboard.Json.Int("guests.total"));
        Assert.Equal(1, dashboard.Json.Int("guests.confirmed"));
        Assert.Equal(1, dashboard.Json.Int("guests.pending"));
        Assert.Equal(1, dashboard.Json.Int("guests.declined"));
    }

    [Fact]
    public async Task Official_total_wins_over_the_sum_of_categories()
    {
        var (owner, w, _) = await NewOwner();
        await owner.Post($"{w}/budget/categories", new { name = "A", plannedAmount = 4000 });
        await owner.Put($"{w}/budget", new { totalBudget = 9000 });

        Assert.Equal(9000, (await owner.Get($"{w}/dashboard")).Json.Dec("financial.totalBudget"));
    }

    [Fact]
    public async Task Shows_next_4_open_installments_and_all_overdue_ones_with_remaining_values()
    {
        var (owner, w, _) = await NewOwner();
        Api.Clock.SetToday(new DateOnly(2030, 6, 15));
        var supplier = (await owner.Post($"{w}/suppliers", new { name = "Luz", category = "Fotografia" })).Json.Id("supplier.id");

        var future = await owner.Post($"{w}/payments", new { description = "Futuro", totalAmount = 600, paymentMethod = "pix", installmentCount = 6, firstDueDate = "2030-07-01", supplierId = supplier });
        await owner.Post($"{w}/installments/{future.Json.Id("installments.0.id")}/settlements", new { amount = 40 });
        await owner.Post($"{w}/installments/{future.Json.Id("installments.1.id")}/settlements", new { amount = 100 });

        var overdue = await owner.Post($"{w}/payments", new { description = "Atrasado", totalAmount = 500, paymentMethod = "pix", installmentCount = 5, firstDueDate = "2030-01-10" });
        await owner.Post($"{w}/installments/{overdue.Json.Id("installments.0.id")}/settlements", new { amount = 100 });
        await owner.Post($"{w}/installments/{overdue.Json.Id("installments.1.id")}/settlements", new { amount = 30 });

        var dashboard = await owner.Get($"{w}/dashboard");
        var upcoming = dashboard.Json.At("upcomingInstallments");
        var late = dashboard.Json.At("overdueInstallments");

        Assert.Equal(4, upcoming.Count());
        Assert.Equal("2030-07-01", upcoming.Str("0.dueDate"));
        Assert.Equal(60, upcoming.Dec("0.remainingAmount"));
        Assert.Equal("partial", upcoming.Str("0.status"));
        Assert.Equal("Luz", upcoming.Str("0.supplierName"));
        Assert.Equal("2030-09-01", upcoming.Str("1.dueDate"));
        Assert.Equal("pending", upcoming.Str("1.status"));
        Assert.Equal(4, late.Count());
        Assert.Equal(70, late.Dec("0.remainingAmount"));
        Assert.All(late.EnumerateArray(), item => Assert.Equal("overdue", item.Str("status")));
    }

    [Fact]
    public async Task Shows_5_nearest_open_tasks_within_30_days_including_late_ones()
    {
        var (owner, w, _) = await NewOwner();
        Api.Clock.SetToday(new DateOnly(2030, 6, 1));
        await owner.Post($"{w}/checklist/items", new { title = "Atrasada", dueDate = "2030-05-20" });
        await owner.Post($"{w}/checklist/items", new { title = "Concluída", dueDate = "2030-06-02", status = "done" });
        await owner.Post($"{w}/checklist/items", new { title = "Sem prazo" });
        await owner.Post($"{w}/checklist/items", new { title = "Fora da janela", dueDate = "2030-07-02" });
        await owner.Post($"{w}/checklist/items", new { title = "Limite", dueDate = "2030-07-01" });

        var few = await owner.Get($"{w}/dashboard");

        await owner.Post($"{w}/checklist/items", new { title = "T1", dueDate = "2030-06-03" });
        await owner.Post($"{w}/checklist/items", new { title = "T2", dueDate = "2030-06-04" });
        await owner.Post($"{w}/checklist/items", new { title = "T3", dueDate = "2030-06-05" });
        await owner.Post($"{w}/checklist/items", new { title = "T4", dueDate = "2030-06-06" });

        var many = await owner.Get($"{w}/dashboard");

        Assert.Equal(["Atrasada", "Limite"], few.Json.At("upcomingTasks").EnumerateArray().Select(t => t.Str("title")));
        Assert.Equal(["Atrasada", "T1", "T2", "T3", "T4"], many.Json.At("upcomingTasks").EnumerateArray().Select(t => t.Str("title")));
    }
}
