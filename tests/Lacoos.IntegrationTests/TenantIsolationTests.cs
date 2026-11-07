using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

/// <summary>Um membro do casamento B tenta usar, pela rota de B, registros do casamento A.</summary>
public sealed class TenantIsolationTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Records_of_another_wedding_are_not_found_through_your_own_wedding()
    {
        var (a, wa, _) = await NewOwner();
        var (b, wb, _) = await NewOwner();

        var task = await a.Post($"{wa}/checklist/items", new { title = "Segredo" });
        var checklistCategory = await a.Post($"{wa}/checklist/categories", new { name = "Cat" });
        var budgetCategory = await a.Post($"{wa}/budget/categories", new { name = "Cat", plannedAmount = 10 });
        var budgetItem = await a.Post($"{wa}/budget/items", new { name = "Item", categoryId = budgetCategory.Json.Id() });
        var group = await a.Post($"{wa}/guest-groups", new { name = "Família", category = "family" });
        var guest = await a.Post($"{wa}/guests", new { firstName = "Ana", lastName = "Lima" });
        var supplier = await a.Post($"{wa}/suppliers", new { name = "Foto", category = "Fotografia" });
        var supplierId = supplier.Json.Id("supplier.id");
        var service = await a.Post($"{wa}/suppliers/{supplierId}/services", new { name = "Álbum" });
        var contract = await a.Post($"{wa}/suppliers/{supplierId}/contracts", new { contractNumber = "1" });
        var payment = await a.Post($"{wa}/payments", new { description = "P", totalAmount = 100, paymentMethod = "pix", firstDueDate = "2030-01-01" });
        var installmentId = payment.Json.Id("installments.0.id");
        var giftCategory = await a.Post($"{wa}/gift-categories", new { name = "Casa" });
        var gift = await a.Post($"{wa}/gifts", new { name = "Vaso", categoryId = giftCategory.Json.Id(), desiredQuantity = 2 });
        var giftId = gift.Json.Id("gift.id");
        var reservation = await a.Post($"{wa}/gifts/{giftId}/reservations", new { quantity = 1, personName = "Tia" });
        var receipt = await a.Upload(HttpMethod.Post, $"{wa}/installments/{installmentId}/receipts", Scenario.Pdf, "c.pdf");

        var attempts = new Dictionary<string, Task<ApiResponse>>
        {
            ["GET tarefa"] = b.Get($"{wb}/checklist/items/{task.Json.Id()}"),
            ["PUT tarefa"] = b.Put($"{wb}/checklist/items/{task.Json.Id()}", new { title = "Invadido" }),
            ["DELETE tarefa"] = b.Delete($"{wb}/checklist/items/{task.Json.Id()}"),
            ["PUT categoria checklist"] = b.Put($"{wb}/checklist/categories/{checklistCategory.Json.Id()}", new { name = "X" }),
            ["GET categoria orçamento"] = b.Get($"{wb}/budget/categories/{budgetCategory.Json.Id()}"),
            ["GET item orçamento"] = b.Get($"{wb}/budget/items/{budgetItem.Json.Id()}"),
            ["DELETE item orçamento"] = b.Delete($"{wb}/budget/items/{budgetItem.Json.Id()}"),
            ["GET grupo"] = b.Get($"{wb}/guest-groups/{group.Json.Id()}"),
            ["GET convidado"] = b.Get($"{wb}/guests/{guest.Json.Id()}"),
            ["mark-sent"] = b.Post($"{wb}/guests/{guest.Json.Id()}/mark-sent"),
            ["DELETE convidado"] = b.Delete($"{wb}/guests/{guest.Json.Id()}"),
            ["GET fornecedor"] = b.Get($"{wb}/suppliers/{supplierId}"),
            ["GET pagamentos fornecedor"] = b.Get($"{wb}/suppliers/{supplierId}/payments"),
            ["GET serviço"] = b.Get($"{wb}/suppliers/{supplierId}/services/{service.Json.Id()}"),
            ["GET contrato"] = b.Get($"{wb}/suppliers/{supplierId}/contracts/{contract.Json.Id()}"),
            ["GET pagamento"] = b.Get($"{wb}/payments/{payment.Json.Id()}"),
            ["DELETE pagamento"] = b.Delete($"{wb}/payments/{payment.Json.Id()}"),
            ["PATCH parcela"] = b.Patch($"{wb}/payments/{payment.Json.Id()}/installments/1", new { dueDate = "2031-01-01" }),
            ["baixa"] = b.Post($"{wb}/installments/{installmentId}/settlements", new { amount = 10 }),
            ["estorno"] = b.Post($"{wb}/installments/{installmentId}/reversal", new { confirm = true, reason = "x" }),
            ["histórico"] = b.Get($"{wb}/installments/{installmentId}/settlements"),
            ["GET comprovante"] = b.Get($"{wb}/receipts/{receipt.Json.Id()}"),
            ["GET presente"] = b.Get($"{wb}/gifts/{giftId}"),
            ["reserva"] = b.Post($"{wb}/gifts/{giftId}/reservations", new { quantity = 1, personName = "X" }),
            ["cancelar reserva"] = b.Post($"{wb}/gift-reservations/{reservation.Json.Id()}/cancel"),
            ["GET categoria presente"] = b.Get($"{wb}/gift-categories/{giftCategory.Json.Id()}"),
        };

        foreach (var (name, attempt) in attempts)
        {
            var response = await attempt;
            Assert.True(response.Status == HttpStatusCode.NotFound, $"{name} retornou {response.Code}: {response.Text}");
        }

        Assert.Equal("Segredo", (await a.Get($"{wa}/checklist/items/{task.Json.Id()}")).Json.Str("title"));
        Assert.Equal(0, (await a.Get($"{wa}/payments/{payment.Json.Id()}")).Json.Dec("paidAmount"));
    }

    [Fact]
    public async Task Listings_only_show_your_own_wedding()
    {
        var (a, wa, _) = await NewOwner();
        var (b, wb, _) = await NewOwner();
        await a.Post($"{wa}/checklist/items", new { title = "Só de A" });
        await a.Post($"{wa}/guests", new { firstName = "Ana", lastName = "Lima" });
        await a.Post($"{wa}/suppliers", new { name = "Foto", category = "Fotografia" });
        await a.Post($"{wa}/payments", new { description = "P", totalAmount = 100, paymentMethod = "pix", firstDueDate = "2030-01-01" });

        Assert.Equal(0, (await b.Get($"{wb}/checklist/items")).Json.Int("totals.total"));
        Assert.Equal(0, (await b.Get($"{wb}/guests")).Json.Int("totals.total"));
        Assert.Equal(0, (await b.Get($"{wb}/suppliers")).Json.Int("totals.total"));
        Assert.Equal(0, (await b.Get($"{wb}/payments")).Json.Dec("totals.total"));
        Assert.Equal(0, (await b.Get($"{wb}/dashboard")).Json.Int("checklist.total"));
    }

    [Fact]
    public async Task References_must_belong_to_the_same_wedding()
    {
        var (a, wa, _) = await NewOwner();
        var (b, wb, _) = await NewOwner();

        var memberOfA = (await a.Get($"{wa}/members")).Json.Id("0.id");
        var supplierOfA = (await a.Post($"{wa}/suppliers", new { name = "Foto", category = "Fotografia" })).Json.Id("supplier.id");
        var budgetCategoryOfA = (await a.Post($"{wa}/budget/categories", new { name = "Cat" })).Json.Id();
        var budgetItemOfA = (await a.Post($"{wa}/budget/items", new { name = "Item", categoryId = budgetCategoryOfA })).Json.Id();
        var checklistCategoryOfA = (await a.Post($"{wa}/checklist/categories", new { name = "Cat" })).Json.Id();
        var guestOfA = (await a.Post($"{wa}/guests", new { firstName = "Ana", lastName = "Lima" })).Json.Id();
        var groupOfA = (await a.Post($"{wa}/guest-groups", new { name = "G", category = "work" })).Json.Id();
        var giftCategoryOfA = (await a.Post($"{wa}/gift-categories", new { name = "Casa" })).Json.Id();

        var budgetCategoryOfB = (await b.Post($"{wb}/budget/categories", new { name = "Cat" })).Json.Id();
        var giftCategoryOfB = (await b.Post($"{wb}/gift-categories", new { name = "Casa" })).Json.Id();
        var giftOfB = (await b.Post($"{wb}/gifts", new { name = "Vaso", categoryId = giftCategoryOfB, desiredQuantity = 1 })).Json.Id("gift.id");

        var attempts = new[]
        {
            await b.Post($"{wb}/checklist/items", new { title = "T", assignedToMemberId = memberOfA }),
            await b.Post($"{wb}/checklist/items", new { title = "T", categoryId = checklistCategoryOfA }),
            await b.Post($"{wb}/budget/items", new { name = "I", categoryId = budgetCategoryOfA }),
            await b.Post($"{wb}/budget/items", new { name = "I", categoryId = budgetCategoryOfB, supplierId = supplierOfA }),
            await b.Post($"{wb}/guests", new { firstName = "A", lastName = "B", groupId = groupOfA }),
            await b.Post($"{wb}/payments", new { description = "P", totalAmount = 1, paymentMethod = "pix", firstDueDate = "2030-01-01", supplierId = supplierOfA }),
            await b.Post($"{wb}/payments", new { description = "P", totalAmount = 1, paymentMethod = "pix", firstDueDate = "2030-01-01", budgetItemId = budgetItemOfA }),
            await b.Post($"{wb}/gifts", new { name = "G", categoryId = giftCategoryOfA, desiredQuantity = 1 }),
            await b.Post($"{wb}/gifts/{giftOfB}/reservations", new { quantity = 1, guestId = guestOfA }),
        };

        Assert.All(attempts, attempt => Assert.Equal(HttpStatusCode.UnprocessableEntity, attempt.Status));
    }
}
